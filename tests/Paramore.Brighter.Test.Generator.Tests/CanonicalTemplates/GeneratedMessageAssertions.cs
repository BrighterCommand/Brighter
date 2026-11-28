#region Licence

/* The MIT License (MIT)
Copyright © 2026 Epi Lo

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Finds each <c>_messageAssertion.Assert(sent, received)</c> call in a generated test and lists the
/// HandledCount steps that come before it in the same block, so a test can check the order of the
/// steps without pinning their text or the names of the two messages.
/// </summary>
internal static class GeneratedMessageAssertions
{
    public const string CHECK_COUNT_AT_LEAST_SENT = "check received HandledCount >= sent";
    public const string COPY_SENT_COUNT = "copy sent HandledCount onto received";
    public const string MESSAGE_ASSERTION = "message assertion";

    public static IReadOnlyList<IReadOnlyList<string>> StepsIn(string generatedSource)
    {
        var root = CSharpSyntaxTree.ParseText(generatedSource).GetRoot();
        var allSteps = new List<IReadOnlyList<string>>();

        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsMessageAssertion(call)) continue;
            if (call.Parent is not ExpressionStatementSyntax { Parent: BlockSyntax block } assertStatement) continue;

            var arguments = call.ArgumentList.Arguments;
            if (arguments.Count != 2) continue;
            var sent = arguments[0].Expression.ToString();
            var received = arguments[1].Expression.ToString();

            var steps = new List<string>();
            foreach (var statement in block.Statements.TakeWhile(s => s != assertStatement))
            {
                if (ChecksCountAtLeastSent(statement, sent, received)) steps.Add(CHECK_COUNT_AT_LEAST_SENT);
                if (CopiesSentCount(statement, sent, received)) steps.Add(COPY_SENT_COUNT);
            }
            steps.Add(MESSAGE_ASSERTION);

            allSteps.Add(steps);
        }

        return allSteps;
    }

    private static bool IsMessageAssertion(InvocationExpressionSyntax call) =>
        call.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.Text: "_messageAssertion" },
            Name.Identifier.Text: "Assert"
        };

    private static bool ChecksCountAtLeastSent(StatementSyntax statement, string sent, string received) =>
        statement.DescendantNodes().OfType<BinaryExpressionSyntax>().Any(compare =>
            compare.IsKind(SyntaxKind.GreaterThanOrEqualExpression)
            && IsHandledCountOf(compare.Left, received)
            && IsHandledCountOf(compare.Right, sent));

    private static bool CopiesSentCount(StatementSyntax statement, string sent, string received) =>
        statement is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
        && IsHandledCountOf(assignment.Left, received)
        && IsHandledCountOf(assignment.Right, sent);

    // message.Header.HandledCount
    private static bool IsHandledCountOf(ExpressionSyntax expression, string messageName) =>
        expression is MemberAccessExpressionSyntax
        {
            Name.Identifier.Text: "HandledCount",
            Expression: MemberAccessExpressionSyntax
            {
                Name.Identifier.Text: "Header",
                Expression: IdentifierNameSyntax message
            }
        }
        && message.Identifier.Text == messageName;
}
