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

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        description: "Setting the Partitioner explicitly makes the choice visible. Be aware that changing the partitioner re-partitions the topic; existing publications can keep the implicit default to preserve their current partition assignment.",
        helpLinkUri: "https://github.com/BrighterCommand/Brighter/blob/master/src/Paramore.Brighter.Analyzer/docs/BRT006.md"
    );

    public static readonly DiagnosticDescriptor ConsistentRandomPartitionerRule = new(
        id: DiagnosticsIds.ConsistentRandomPartitioner,
        title: "ConsistentRandom Partitioner Used",
        messageFormat: "Prefer 'Murmur2Random' over 'ConsistentRandom' for new KafkaPublications",
        category: PartitionerCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "'Murmur2Random' spreads keys more evenly across partitions than the CRC32-based 'ConsistentRandom', avoiding hot partitions. Existing publications can keep 'ConsistentRandom' to preserve their current partition assignment.",
        helpLinkUri: "https://github.com/BrighterCommand/Brighter/blob/master/src/Paramore.Brighter.Analyzer/docs/BRT007.md"
    );

    public static readonly DiagnosticDescriptor ConsistentPartitionerRule = new(
        id: DiagnosticsIds.ConsistentPartitioner,
        title: "Consistent Partitioner Used",
        messageFormat: "Prefer 'Murmur2' over 'Consistent' for new KafkaPublications",
        category: PartitionerCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "'Murmur2' spreads keys more evenly across partitions than the CRC32-based 'Consistent', avoiding hot partitions. Existing publications can keep 'Consistent' to preserve their current partition assignment.",
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
            // Resolve the symbols once and compare by symbol from here on.
            var kafkaPublicationSymbol = compilationContext.Compilation.GetTypeByMetadataName(
                $"{BrighterAnalyzerGlobals.KafkaNamespace}.{BrighterAnalyzerGlobals.KafkaPublicationClassName}");
            var partitionerEnumSymbol = compilationContext.Compilation.GetTypeByMetadataName(
                $"{BrighterAnalyzerGlobals.KafkaNamespace}.{BrighterAnalyzerGlobals.PartitionerEnum}");
            if (kafkaPublicationSymbol == null || partitionerEnumSymbol == null)
            {
                return;
            }

            // Memoises the constructor inspection per type, so a subclass
            // instantiated in many places is only walked once per compilation.
            var constructorCheckCache = new ConcurrentDictionary<INamedTypeSymbol, bool>(SymbolEqualityComparer.Default);

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzerObjectCreation(operationContext, kafkaPublicationSymbol, partitionerEnumSymbol, constructorCheckCache),
                OperationKind.ObjectCreation);
            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeAssignment(operationContext, kafkaPublicationSymbol, partitionerEnumSymbol),
                OperationKind.SimpleAssignment);
        });
    }

    private static void AnalyzerObjectCreation(
        OperationAnalysisContext context,
        INamedTypeSymbol kafkaPublicationSymbol,
        INamedTypeSymbol partitionerEnumSymbol,
        ConcurrentDictionary<INamedTypeSymbol, bool> constructorCheckCache)
    {
        var operation = (IObjectCreationOperation)context.Operation;

        // Cheap rejection before allocating the visitor; most object creations
        // in a compilation are not KafkaPublications.
        if (!KafkaPublicationPartitionerVisitor.IsKafkaPublicationType(operation.Type, kafkaPublicationSymbol))
        {
            return;
        }

        var visitor = new KafkaPublicationPartitionerVisitor(kafkaPublicationSymbol, partitionerEnumSymbol);
        operation.Accept(visitor);

        if (!visitor.IsPartitionerAssigned)
        {
            if (HasPartitionerAssignmentAfterConstruction(operation) ||
                SetsPartitionerInConstructor(operation.Type, kafkaPublicationSymbol, constructorCheckCache, context.CancellationToken))
            {
                // The partitioner is set on the instance after construction or by
                // the type's own constructor; any discouraged value is reported
                // by AnalyzeAssignment instead.
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                MissingPartitionerRule,
                GetCreationLocation(operation.Syntax),
                visitor.PublicationName));
        }
        else if (visitor.IsConsistentRandom)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ConsistentRandomPartitionerRule,
                visitor.PartitionerAssignmentLocation));
        }
        else if (visitor.IsConsistent)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ConsistentPartitionerRule,
                visitor.PartitionerAssignmentLocation));
        }
    }

    // Report on the type name (or the `new` keyword for a target-typed new)
    // rather than the whole creation, so a large initializer isn't squiggled
    // in full.
    private static Location GetCreationLocation(SyntaxNode creationSyntax)
    {
        return creationSyntax switch
        {
            ObjectCreationExpressionSyntax objectCreation => objectCreation.Type.GetLocation(),
            ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.NewKeyword.GetLocation(),
            _ => creationSyntax.GetLocation()
        };
    }

    // A subclass can set the partitioner in its own constructor, e.g.:
    //     class OrdersPublication : KafkaPublication
    //     {
    //         public OrdersPublication() { Partitioner = Partitioner.Murmur2Random; }
    //     }
    // Don't report BRT006 for such a type — an initializer added by the code fix
    // would override the subclass's deliberate choice. Only constructors declared
    // below KafkaPublication itself are considered; its own Partitioner default is
    // exactly what BRT006 flags as implicit.
    private static bool SetsPartitionerInConstructor(
        ITypeSymbol type,
        INamedTypeSymbol kafkaPublicationSymbol,
        ConcurrentDictionary<INamedTypeSymbol, bool> constructorCheckCache,
        CancellationToken cancellationToken)
    {
        for (var current = type as INamedTypeSymbol;
             current != null && !SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, kafkaPublicationSymbol);
             current = current.BaseType)
        {
            if (!constructorCheckCache.TryGetValue(current, out var assigns))
            {
                assigns = current.InstanceConstructors.Any(constructor => ConstructorAssignsPartitioner(constructor, cancellationToken));
                constructorCheckCache[current] = assigns;
            }

            if (assigns)
            {
                return true;
            }
        }

        return false;
    }

    // Syntactic check (analyzers must not call Compilation.GetSemanticModel, RS1030):
    // an assignment to `Partitioner` or `this.Partitioner` in the constructor body.
    // In a KafkaPublication subclass constructor an unqualified `Partitioner` can
    // only bind to the inherited property or a local of the same name — the latter
    // is contrived and accepted.
    private static bool ConstructorAssignsPartitioner(IMethodSymbol constructor, CancellationToken cancellationToken)
    {
        foreach (var syntaxReference in constructor.DeclaringSyntaxReferences)
        {
            var assignsPartitioner = syntaxReference.GetSyntax(cancellationToken)
                .DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Any(assignment => assignment.Left switch
                {
                    IdentifierNameSyntax id => id.Identifier.ValueText == BrighterAnalyzerGlobals.PartitionerProperty,
                    MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } memberAccess =>
                        memberAccess.Name.Identifier.ValueText == BrighterAnalyzerGlobals.PartitionerProperty,
                    _ => false
                });

            if (assignsPartitioner)
            {
                return true;
            }
        }

        return false;
    }

    // Reports discouraged partitioner values assigned outside an object
    // initializer, e.g.:
    //     var publication = new KafkaPublication();
    //     publication.Partitioner = Partitioner.Consistent;
    // The diagnostic is reported from the assignment's own callback so it stays
    // local to the analyzed operation.
    private static void AnalyzeAssignment(
        OperationAnalysisContext context,
        INamedTypeSymbol kafkaPublicationSymbol,
        INamedTypeSymbol partitionerEnumSymbol)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;

        // Assignments inside an object creation's initializer
        // (new KafkaPublication { Partitioner = ... }) are handled by
        // AnalyzerObjectCreation. Assignments in a nested member initializer
        // (new Holder { Publication = { Partitioner = ... } }) — whose parent
        // initializer hangs off an IMemberInitializerOperation, not a creation —
        // ARE handled here.
        if (assignment.Parent is IObjectOrCollectionInitializerOperation { Parent: not IMemberInitializerOperation })
        {
            return;
        }

        if (assignment.Target is not IPropertyReferenceOperation propertyReference ||
            propertyReference.Property.Name != BrighterAnalyzerGlobals.PartitionerProperty ||
            !KafkaPublicationPartitionerVisitor.IsKafkaPublicationType(propertyReference.Property.ContainingType, kafkaPublicationSymbol))
        {
            return;
        }

        switch (KafkaPublicationPartitionerVisitor.GetPartitionerValueName(assignment.Value, partitionerEnumSymbol))
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

    // Checks whether the partitioner is assigned on the just-created instance
    // later in the same block, e.g.:
    //     var publication = new KafkaPublication();
    //     publication.Partitioner = Partitioner.Murmur2Random;
    // Works for locals, fields, properties and parameters. Assignments made
    // before the construction, or elsewhere (helper methods, other blocks),
    // are not tracked.
    private static bool HasPartitionerAssignmentAfterConstruction(IObjectCreationOperation operation)
    {
        ISymbol symbol = operation.Parent switch
        {
            IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator } => declarator.Symbol,
            ISimpleAssignmentOperation { Target: ILocalReferenceOperation localReference } => localReference.Local,
            ISimpleAssignmentOperation { Target: IFieldReferenceOperation fieldReference } => fieldReference.Field,
            ISimpleAssignmentOperation { Target: IPropertyReferenceOperation propertyReference } => propertyReference.Property,
            _ => null
        };

        if (symbol == null)
        {
            return false;
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
            return false;
        }

        return block.Operations
            .OfType<IExpressionStatementOperation>()
            .Select(statement => statement.Operation)
            .OfType<ISimpleAssignmentOperation>()
            // Field/property targets are compared by symbol, not instance: an
            // assignment through another object sharing the field (a.Pub vs b.Pub)
            // would also match — an accepted, contrived edge case.
            .Any(assignment =>
                assignment.Syntax.SpanStart > operation.Syntax.SpanStart &&
                assignment.Target is IPropertyReferenceOperation propertyReference &&
                propertyReference.Property.Name == BrighterAnalyzerGlobals.PartitionerProperty &&
                IsReferenceTo(propertyReference.Instance, symbol));
    }

    private static bool IsReferenceTo(IOperation instance, ISymbol symbol)
    {
        return instance switch
        {
            ILocalReferenceOperation localReference => SymbolEqualityComparer.Default.Equals(localReference.Local, symbol),
            IFieldReferenceOperation fieldReference => SymbolEqualityComparer.Default.Equals(fieldReference.Field, symbol),
            IPropertyReferenceOperation propertyReference => SymbolEqualityComparer.Default.Equals(propertyReference.Property, symbol),
            IParameterReferenceOperation parameterReference => SymbolEqualityComparer.Default.Equals(parameterReference.Parameter, symbol),
            _ => false
        };
    }
}
