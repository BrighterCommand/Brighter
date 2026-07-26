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
using Microsoft.CodeAnalysis.Simplification;

namespace Paramore.Brighter.Analyzer.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingPartitionerCodeFixProvider)), Shared]
public class MissingPartitionerCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [DiagnosticsIds.MissingPartitioner];

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
            var objectCreation = root.FindNode(diagnostic.Location.SourceSpan)
                .DescendantNodesAndSelf()
                .OfType<BaseObjectCreationExpressionSyntax>()
                .FirstOrDefault();

            if (objectCreation == null)
            {
                continue;
            }

            var target = BrighterAnalyzerGlobals.Murmur2RandomPartitionerValue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Set 'Partitioner' to 'Partitioner.{target}'",
                    createChangedDocument: ct => AddPartitionerAsync(context.Document, objectCreation, target, ct),
                    equivalenceKey: nameof(MissingPartitionerCodeFixProvider)),
                diagnostic);
        }
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
                    SyntaxFactory.ParseExpression($"{BrighterAnalyzerGlobals.KafkaNamespace}.{BrighterAnalyzerGlobals.PartitionerEnum}"),
                    SyntaxFactory.IdentifierName(target))
                .WithAdditionalAnnotations(Simplifier.Annotation));

        var initializer = objectCreation.Initializer == null
            ? SyntaxFactory.InitializerExpression(
                SyntaxKind.ObjectInitializerExpression,
                SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(assignment))
            : AddInitializerExpression(objectCreation.Initializer, assignment);

        var newObjectCreation = objectCreation
            .WithInitializer(initializer)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root!.ReplaceNode(objectCreation, newObjectCreation);
        var formatted = await Formatter.FormatAsync(document.WithSyntaxRoot(newRoot), Formatter.Annotation, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Reduce the fully qualified Partitioner reference where the using is
        // already present; otherwise keep it qualified so the fix always compiles.
        return await Simplifier.ReduceAsync(formatted, Simplifier.Annotation, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static InitializerExpressionSyntax AddInitializerExpression(
        InitializerExpressionSyntax initializer,
        ExpressionSyntax expression)
    {
        // InitializerExpressionSyntax.AddExpressions inserts the separator comma right
        // after the last expression but before its trailing trivia, so the comma ends
        // up on the wrong line. Rewire the trivia by hand: the newline + indent that
        // follows an existing separator (or the open brace) leads the new expression,
        // and the newline before the closing brace moves behind the new expression.
        // Comments stay with the expression they document: anything before the final
        // newline (e.g. "// one per shard") becomes the separator's trailing trivia.
        var lastExpression = initializer.Expressions.Last();
        var trailingTrivia = lastExpression.GetTrailingTrivia();

        var commentTrivia = trailingTrivia.TakeWhile(t => !t.IsKind(SyntaxKind.EndOfLineTrivia));
        var endOfLineTrivia = trailingTrivia.SkipWhile(t => !t.IsKind(SyntaxKind.EndOfLineTrivia));

        var leadingTrivia = initializer.Expressions.Count > 1
            ? initializer.Expressions.GetSeparator(initializer.Expressions.Count - 2).TrailingTrivia
            : initializer.OpenBraceToken.TrailingTrivia;

        var newExpression = expression
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(endOfLineTrivia);

        var expressions = initializer.Expressions
            .Replace(lastExpression, lastExpression.WithoutTrailingTrivia())
            .Add(newExpression);

        if (commentTrivia.Any())
        {
            var nodesAndTokens = expressions.GetWithSeparators();
            var separator = nodesAndTokens[nodesAndTokens.Count - 2].AsToken().WithTrailingTrivia(commentTrivia);
            nodesAndTokens = nodesAndTokens.Replace(nodesAndTokens[nodesAndTokens.Count - 2], separator);
            expressions = SyntaxFactory.SeparatedList<ExpressionSyntax>(nodesAndTokens);
        }

        return initializer.WithExpressions(expressions);
    }
}
