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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Paramore.Brighter.Analyzer.Visitors.Operation;

namespace Paramore.Brighter.Analyzer.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class KafkaPublicationPartitionerAnalyzer : DiagnosticAnalyzer
{
    private const string PartitionerCategory = "Design";

    public static readonly DiagnosticDescriptor s_missingPartitionerRule = new(
        id: DiagnosticsIds.MissingPartitioner,
        title: "Missing Partitioner",
        messageFormat: "Partitioner assignment is missing from {0}. Consider setting it explicitly.",
        category: PartitionerCategory,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor s_consistentRandomPartitionerRule = new(
        id: DiagnosticsIds.ConsistentRandomPartitioner,
        title: "ConsistentRandom Partitioner Used",
        messageFormat:
        "Prefer 'Murmur2Random' over 'ConsistentRandom' for new KafkaPublications. (Existing publications can safely ignore this warning).",
        category: PartitionerCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor s_consistentPartitionerRule = new(
        id: DiagnosticsIds.ConsistentPartitioner,
        title: "Consistent Partitioner Used",
        messageFormat:
        "Prefer 'Murmur2' over 'Consistent' for new KafkaPublications. (Existing publications can safely ignore this warning).",
        category: PartitionerCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_missingPartitionerRule, s_consistentRandomPartitionerRule, s_consistentPartitionerRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzerOperation, OperationKind.ObjectCreation);
    }

    private static void AnalyzerOperation(OperationAnalysisContext context)
    {
        var visitor = new KafkaPublicationPartitionerVisitor();
        context.Operation.Accept(visitor);

        if (!visitor.IsKafkaPublication)
        {
            return;
        }

        if (!visitor.IsPartitionerAssigned)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                s_missingPartitionerRule,
                context.Operation.Syntax.GetLocation(),
                visitor.PublicationName));
        }
        else if (visitor.IsConsistentRandom)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                s_consistentRandomPartitionerRule,
                context.Operation.Syntax.GetLocation()));
        }
        else if (visitor.IsConsistent)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                s_consistentPartitionerRule,
                context.Operation.Syntax.GetLocation()));
        }
    }
}
