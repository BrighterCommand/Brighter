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
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paramore.Brighter.Analyzer.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PartitionerValueCodeFixProvider)), Shared]
public class PartitionerValueCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [DiagnosticsIds.ConsistentRandomPartitioner, DiagnosticsIds.ConsistentPartitioner];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var target = diagnostic.Id == DiagnosticsIds.ConsistentRandomPartitioner
                ? BrighterAnalyzerGlobals.Murmur2RandomPartitionerValue
                : BrighterAnalyzerGlobals.Murmur2PartitionerValue;

            var assignment = root.FindNode(diagnostic.Location.SourceSpan)
                .DescendantNodesAndSelf()
                .OfType<AssignmentExpressionSyntax>()
                .FirstOrDefault(a => a.Left is IdentifierNameSyntax id &&
                                     id.Identifier.ValueText == BrighterAnalyzerGlobals.PartitionerProperty);

            if (assignment == null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Use 'Partitioner.{target}'",
                    createChangedDocument: ct => ReplacePartitionerValueAsync(context.Document, assignment, target, ct),
                    equivalenceKey: $"{nameof(PartitionerValueCodeFixProvider)}:{target}"),
                diagnostic);
        }
    }

    private static async Task<Document> ReplacePartitionerValueAsync(
        Document document,
        AssignmentExpressionSyntax assignment,
        string target,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        var newName = SyntaxFactory.IdentifierName(target);
        ExpressionSyntax newValue = assignment.Right switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.WithName(newName),
            _ => newName
        };

        var newRoot = root!.ReplaceNode(
            assignment.Right,
            newValue.WithTriviaFrom(assignment.Right));

        return document.WithSyntaxRoot(newRoot);
    }
}
