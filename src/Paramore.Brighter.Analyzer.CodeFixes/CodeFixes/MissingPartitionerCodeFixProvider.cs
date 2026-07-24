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
using Microsoft.CodeAnalysis.Formatting;

namespace Paramore.Brighter.Analyzer.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingPartitionerCodeFixProvider)), Shared]
public class MissingPartitionerCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [DiagnosticsIds.MissingPartitioner];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var objectCreation = root?.FindNode(context.Diagnostics[0].Location.SourceSpan)
            .DescendantNodesAndSelf()
            .OfType<BaseObjectCreationExpressionSyntax>()
            .FirstOrDefault();

        if (objectCreation == null)
        {
            return;
        }

        var target = BrighterAnalyzerGlobals.Murmur2RandomPartitionerValue;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Set 'Partitioner' to 'Partitioner.{target}'",
                createChangedDocument: ct => AddPartitionerAsync(context.Document, objectCreation, target, ct),
                equivalenceKey: nameof(MissingPartitionerCodeFixProvider)),
            context.Diagnostics[0]);
    }

    private static async Task<Document> AddPartitionerAsync(
        Document document,
        BaseObjectCreationExpressionSyntax objectCreation,
        string target,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        var assignment = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName(BrighterAnalyzerGlobals.PartitionerProperty),
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(BrighterAnalyzerGlobals.PartitionerEnum),
                SyntaxFactory.IdentifierName(target)));

        var initializer = objectCreation.Initializer == null
            ? SyntaxFactory.InitializerExpression(
                SyntaxKind.ObjectInitializerExpression,
                SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(assignment))
            : objectCreation.Initializer.AddExpressions(assignment);

        var newObjectCreation = objectCreation
            .WithInitializer(initializer)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root!.ReplaceNode(objectCreation, newObjectCreation);
        var formatted = Formatter.Format(newRoot, Formatter.Annotation, document.Project.Solution.Workspace, cancellationToken: cancellationToken);

        return document.WithSyntaxRoot(formatted);
    }
}
