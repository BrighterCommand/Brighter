#region License

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Paramore.Brighter.Analyzer.Visitors.Operation;

namespace Paramore.Brighter.Analyzer.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class KafkaPublicationPartitionerAnalyzer : DiagnosticAnalyzer
{
    private const string PartitionerCategory = "Design";

    public static readonly DiagnosticDescriptor MissingPartitionerRule = new(
        id: DiagnosticsIds.MissingPartitioner,
        title: "Missing Partitioner",
        messageFormat: "Partitioner assignment is missing from {0}. Consider setting it explicitly.",
        category: PartitionerCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/BrighterCommand/Brighter/blob/master/src/Paramore.Brighter.Analyzer/docs/BRT006.md"
    );

    public static readonly DiagnosticDescriptor ConsistentRandomPartitionerRule = new(
        id: DiagnosticsIds.ConsistentRandomPartitioner,
        title: "ConsistentRandom Partitioner Used",
        messageFormat: "Prefer 'Murmur2Random' over 'ConsistentRandom' for new KafkaPublications",
        category: PartitionerCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "'ConsistentRandom' can produce uneven key distribution and hot partitions; 'Murmur2Random' keeps distribution even. Existing publications can keep 'ConsistentRandom' to preserve their current partition assignment.",
        helpLinkUri: "https://github.com/BrighterCommand/Brighter/blob/master/src/Paramore.Brighter.Analyzer/docs/BRT007.md"
    );

    public static readonly DiagnosticDescriptor ConsistentPartitionerRule = new(
        id: DiagnosticsIds.ConsistentPartitioner,
        title: "Consistent Partitioner Used",
        messageFormat: "Prefer 'Murmur2' over 'Consistent' for new KafkaPublications",
        category: PartitionerCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "'Consistent' can produce uneven key distribution and hot partitions; 'Murmur2' keeps distribution even. Existing publications can keep 'Consistent' to preserve their current partition assignment.",
        helpLinkUri: "https://github.com/BrighterCommand/Brighter/blob/master/src/Paramore.Brighter.Analyzer/docs/BRT008.md"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [MissingPartitionerRule, ConsistentRandomPartitionerRule, ConsistentPartitionerRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            // Solutions that don't reference the Kafka gateway can never create a
            // KafkaPublication; don't pay for an operation callback there at all.
            if (compilationContext.Compilation.GetTypeByMetadataName(
                    $"{BrighterAnalyzerGlobals.KafkaNamespace}.{BrighterAnalyzerGlobals.KafkaPublicationClassName}") == null)
            {
                return;
            }

            compilationContext.RegisterOperationAction(AnalyzerObjectCreation, OperationKind.ObjectCreation);
            compilationContext.RegisterOperationAction(AnalyzeAssignment, OperationKind.SimpleAssignment);
        });
    }

    private static void AnalyzerObjectCreation(OperationAnalysisContext context)
    {
        var operation = (IObjectCreationOperation)context.Operation;

        // Cheap rejection before allocating the visitor; most object creations
        // in a compilation are not KafkaPublications.
        if (!KafkaPublicationPartitionerVisitor.IsKafkaPublicationType(operation.Type))
        {
            return;
        }

        var visitor = new KafkaPublicationPartitionerVisitor();
        operation.Accept(visitor);

        if (!visitor.IsPartitionerAssigned)
        {
            if (FindPartitionerAssignmentAfterConstruction(operation) != null)
            {
                // The partitioner is set on the local after construction; any
                // discouraged value is reported by AnalyzeAssignment instead.
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                MissingPartitionerRule,
                context.Operation.Syntax.GetLocation(),
                visitor.PublicationName));
        }
        else if (visitor.IsConsistentRandom)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ConsistentRandomPartitionerRule,
                context.Operation.Syntax.GetLocation()));
        }
        else if (visitor.IsConsistent)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ConsistentPartitionerRule,
                context.Operation.Syntax.GetLocation()));
        }
    }

    // Reports discouraged partitioner values assigned outside an object
    // initializer, e.g.:
    //     var publication = new KafkaPublication();
    //     publication.Partitioner = Partitioner.Consistent;
    // The diagnostic is reported from the assignment's own callback so it stays
    // local to the analyzed operation.
    private static void AnalyzeAssignment(OperationAnalysisContext context)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;

        // Initializer assignments (new KafkaPublication { Partitioner = ... }) are
        // handled by AnalyzerObjectCreation. Their parent is an
        // IObjectOrCollectionInitializerOperation; IMemberInitializerOperation
        // only appears in `with` expressions.
        if (assignment.Parent is IObjectOrCollectionInitializerOperation or IMemberInitializerOperation)
        {
            return;
        }

        if (assignment.Target is not IPropertyReferenceOperation propertyReference ||
            propertyReference.Property.Name != BrighterAnalyzerGlobals.PartitionerProperty ||
            !KafkaPublicationPartitionerVisitor.IsKafkaPublicationType(propertyReference.Property.ContainingType))
        {
            return;
        }

        switch (KafkaPublicationPartitionerVisitor.GetPartitionerValueName(assignment.Value))
        {
            case BrighterAnalyzerGlobals.ConsistentRandomPartitionerValue:
                context.ReportDiagnostic(Diagnostic.Create(
                    ConsistentRandomPartitionerRule,
                    assignment.Syntax.GetLocation()));
                break;
            case BrighterAnalyzerGlobals.ConsistentPartitionerValue:
                context.ReportDiagnostic(Diagnostic.Create(
                    ConsistentPartitionerRule,
                    assignment.Syntax.GetLocation()));
                break;
        }
    }

    // Finds a partitioner assignment made on the new local right after
    // construction, e.g.:
    //     var publication = new KafkaPublication();
    //     publication.Partitioner = Partitioner.Murmur2Random;
    // Returns null when there is no such assignment. Assignments made
    // elsewhere (helper methods, other blocks) are not tracked.
    private static ISimpleAssignmentOperation FindPartitionerAssignmentAfterConstruction(IObjectCreationOperation operation)
    {
        ILocalSymbol local = operation.Parent switch
        {
            IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator } => declarator.Symbol,
            ISimpleAssignmentOperation { Target: ILocalReferenceOperation localReference } => localReference.Local,
            _ => null
        };

        if (local == null)
        {
            return null;
        }

        // Only the nearest enclosing block is searched; an assignment inside a
        // nested block (e.g. an if) still triggers BRT006 — a documented
        // limitation (see BRT006.md).
        var ancestor = operation.Parent;
        while (ancestor != null && ancestor is not IBlockOperation)
        {
            ancestor = ancestor.Parent;
        }

        if (ancestor is not IBlockOperation block)
        {
            return null;
        }

        return block.Operations
            .OfType<IExpressionStatementOperation>()
            .Select(statement => statement.Operation)
            .OfType<ISimpleAssignmentOperation>()
            .FirstOrDefault(assignment =>
                assignment.Target is IPropertyReferenceOperation propertyReference &&
                propertyReference.Property.Name == BrighterAnalyzerGlobals.PartitionerProperty &&
                propertyReference.Instance is ILocalReferenceOperation instance &&
                SymbolEqualityComparer.Default.Equals(instance.Local, local));
    }
}
