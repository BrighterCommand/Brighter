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
                .AncestorsAndSelf()
                .OfType<BaseObjectCreationExpressionSyntax>()
                .FirstOrDefault();

            if (objectCreation == null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Set 'Partitioner' to 'Partitioner.{BrighterAnalyzerGlobals.Murmur2RandomPartitionerValue}' (re-partitions the topic)",
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

        InitializerExpressionSyntax initializer;
        if (objectCreation.Initializer == null)
        {
            initializer = SyntaxFactory.InitializerExpression(
                SyntaxKind.ObjectInitializerExpression,
                SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(assignment));
        }
        else if (objectCreation.Initializer.Expressions.Count == 0)
        {
            // new KafkaPublication { } — keep the existing (empty) braces and trivia.
            initializer = objectCreation.Initializer.WithExpressions(
                SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(assignment));
        }
        else
        {
            initializer = AddInitializerExpression(objectCreation.Initializer, assignment);
        }

        var newObjectCreation = objectCreation.WithInitializer(initializer);

        // Drop a now-redundant empty argument list: with an initializer present,
        // new KafkaPublication { ... } reads better than new KafkaPublication() { ... }.
        // Keep the argument list's trailing trivia (the space before the brace).
        if (newObjectCreation is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0 } explicitCreation)
        {
            newObjectCreation = explicitCreation
                .WithType(explicitCreation.Type.WithTrailingTrivia(explicitCreation.ArgumentList.GetTrailingTrivia()))
                .WithArgumentList(null);
        }

        newObjectCreation = newObjectCreation.WithAdditionalAnnotations(Formatter.Annotation);

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
        // Build the new expression list by hand: AddExpressions would insert the
        // separator comma right after the last expression but before its trailing
        // trivia, so the comma lands on the wrong line and any trailing comment
        // (e.g. "// one per shard") would move onto the new expression.
        var nodesAndTokens = initializer.Expressions.GetWithSeparators();

        // A trailing comma ("{ A = 1, B = 2, }") carries the closing-brace trivia:
        // drop the comma and move its trivia onto the last expression.
        if (nodesAndTokens.Count > 0 && nodesAndTokens[nodesAndTokens.Count - 1].IsToken)
        {
            var trailingComma = nodesAndTokens[nodesAndTokens.Count - 1].AsToken();
            nodesAndTokens = nodesAndTokens.RemoveAt(nodesAndTokens.Count - 1);
            nodesAndTokens = nodesAndTokens.Replace(
                nodesAndTokens[nodesAndTokens.Count - 1],
                ((ExpressionSyntax)nodesAndTokens[nodesAndTokens.Count - 1].AsNode()!)
                    .WithTrailingTrivia(trailingComma.TrailingTrivia));
        }

        var lastExpression = (ExpressionSyntax)nodesAndTokens[nodesAndTokens.Count - 1].AsNode()!;
        var trailingTrivia = lastExpression.GetTrailingTrivia();
        var beforeEndOfLine = trailingTrivia.TakeWhile(t => !t.IsKind(SyntaxKind.EndOfLineTrivia)).ToList();
        var fromEndOfLine = trailingTrivia.SkipWhile(t => !t.IsKind(SyntaxKind.EndOfLineTrivia)).ToList();

        var separator = SyntaxFactory.Token(SyntaxKind.CommaToken);
        ExpressionSyntax newExpression;
        if (fromEndOfLine.Count == 0)
        {
            // Single-line initializer ("{ Topic = x }"): keep it on one line,
            // with single spaces around the new expression.
            var hasComment = beforeEndOfLine.Any(IsComment);
            if (hasComment)
            {
                separator = separator.WithTrailingTrivia(beforeEndOfLine);
            }

            newExpression = expression
                .WithLeadingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Space))
                .WithTrailingTrivia(hasComment
                    ? SyntaxFactory.TriviaList(SyntaxFactory.Space)
                    : SyntaxFactory.TriviaList(beforeEndOfLine));
        }
        else
        {
            // Multi-line: comments stay on their line attached to the comma, the
            // newline + indent that follows an existing separator (or the open
            // brace) leads the new expression, and the newline before the closing
            // brace moves behind it.
            separator = separator.WithTrailingTrivia(beforeEndOfLine);
            var leadingTrivia = nodesAndTokens.Count > 1
                ? nodesAndTokens[nodesAndTokens.Count - 2].AsToken().TrailingTrivia
                : initializer.OpenBraceToken.TrailingTrivia;
            newExpression = expression
                .WithLeadingTrivia(leadingTrivia)
                .WithTrailingTrivia(fromEndOfLine);
        }

        return initializer.WithExpressions(
            SyntaxFactory.SeparatedList<ExpressionSyntax>(
                nodesAndTokens
                    .Replace(nodesAndTokens[nodesAndTokens.Count - 1], lastExpression.WithoutTrailingTrivia())
                    .Add(separator)
                    .Add(newExpression)));
    }

    private static bool IsComment(SyntaxTrivia trivia)
    {
        return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);
    }
}
