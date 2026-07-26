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

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Set 'Partitioner' to 'Partitioner.{BrighterAnalyzerGlobals.Murmur2RandomPartitionerValue}'",
                    createChangedDocument: ct => AddPartitionerAsync(context.Document, objectCreation, ct),
                    equivalenceKey: nameof(MissingPartitionerCodeFixProvider)),
                diagnostic);
        }
    }

    private static async Task<Document> AddPartitionerAsync(
        Document document,
        BaseObjectCreationExpressionSyntax objectCreation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        var assignment = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName(BrighterAnalyzerGlobals.PartitionerProperty),
            SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseExpression($"{BrighterAnalyzerGlobals.KafkaNamespace}.{BrighterAnalyzerGlobals.PartitionerEnum}"),
                    SyntaxFactory.IdentifierName(BrighterAnalyzerGlobals.Murmur2RandomPartitionerValue))
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

        var beforeEndOfLine = trailingTrivia.TakeWhile(t => !t.IsKind(SyntaxKind.EndOfLineTrivia)).ToList();
        var fromEndOfLine = trailingTrivia.SkipWhile(t => !t.IsKind(SyntaxKind.EndOfLineTrivia)).ToList();

        SyntaxTriviaList separatorTrailingTrivia;
        ExpressionSyntax newExpression;
        if (fromEndOfLine.Count == 0)
        {
            // Single-line initializer ("{ Topic = x }"): keep it on one line,
            // with single spaces around the new expression.
            separatorTrailingTrivia = beforeEndOfLine.Any(IsComment)
                ? SyntaxFactory.TriviaList(beforeEndOfLine)
                : default;
            newExpression = expression
                .WithLeadingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Space))
                .WithTrailingTrivia(separatorTrailingTrivia.Count > 0
                    ? SyntaxFactory.TriviaList(SyntaxFactory.Space)
                    : SyntaxFactory.TriviaList(beforeEndOfLine));
        }
        else
        {
            separatorTrailingTrivia = SyntaxFactory.TriviaList(beforeEndOfLine);
            var leadingTrivia = initializer.Expressions.Count > 1
                ? initializer.Expressions.GetSeparator(initializer.Expressions.Count - 2).TrailingTrivia
                : initializer.OpenBraceToken.TrailingTrivia;
            newExpression = expression
                .WithLeadingTrivia(leadingTrivia)
                .WithTrailingTrivia(fromEndOfLine);
        }

        var expressions = initializer.Expressions
            .Replace(lastExpression, lastExpression.WithoutTrailingTrivia())
            .Add(newExpression);

        if (separatorTrailingTrivia.Count > 0)
        {
            var nodesAndTokens = expressions.GetWithSeparators();
            var separator = nodesAndTokens[nodesAndTokens.Count - 2].AsToken().WithTrailingTrivia(separatorTrailingTrivia);
            nodesAndTokens = nodesAndTokens.Replace(nodesAndTokens[nodesAndTokens.Count - 2], separator);
            expressions = SyntaxFactory.SeparatedList<ExpressionSyntax>(nodesAndTokens);
        }

        return initializer.WithExpressions(expressions);
    }

    private static bool IsComment(SyntaxTrivia trivia)
    {
        return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);
    }
}
