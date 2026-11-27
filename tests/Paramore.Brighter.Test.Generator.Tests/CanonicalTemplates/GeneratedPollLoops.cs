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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Finds the poll loops in a generated test, so a test can check how long it waits and how often it
/// reads, not how the loop is written.
/// </summary>
internal static class GeneratedPollLoops
{
    // ADR 0066: arrivals are read every 500 ms, up to 30 s on a channel and 60 s on a rejection destination
    public static readonly PollLoop ChannelArrival = new(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(500));
    public static readonly PollLoop RejectionArrival = new(TimeSpan.FromSeconds(60), TimeSpan.FromMilliseconds(500));

    public static IReadOnlyList<PollLoop> In(string generatedSource)
    {
        var root = CSharpSyntaxTree.ParseText(generatedSource).GetRoot();
        var pollLoops = new List<PollLoop>();

        foreach (var loop in root.DescendantNodes().OfType<WhileStatementSyntax>())
        {
            var ceiling = CeilingOf(loop.Condition);
            // a loop with no time ceiling is not waiting for an arrival, e.g. the send retry
            if (ceiling is null) continue;

            pollLoops.Add(new PollLoop(ceiling.Value, IntervalOf(loop.Statement)));
        }

        return pollLoops;
    }

    private static TimeSpan? CeilingOf(ExpressionSyntax condition)
    {
        // stopwatch.Elapsed < TimeSpan.FromSeconds(30), alone or joined with && to another condition
        foreach (var comparison in condition.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>())
        {
            if (!comparison.IsKind(SyntaxKind.LessThanExpression)) continue;

            var ceiling = ReadTimeSpan(comparison.Right);
            if (ceiling is not null) return ceiling;
        }

        return null;
    }

    private static TimeSpan IntervalOf(StatementSyntax loopBody)
    {
        // the first wait in the body: Receive(TimeSpan.FromMilliseconds(500)), or Thread.Sleep(500) / Task.Delay(500)
        foreach (var call in loopBody.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var firstArgument = call.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (firstArgument is null) continue;

            var timeout = ReadTimeSpan(firstArgument);
            if (timeout is not null) return timeout.Value;

            if (IsSleepOrDelay(call) && ReadNumber(firstArgument) is { } milliseconds)
                return TimeSpan.FromMilliseconds(milliseconds);
        }

        return TimeSpan.Zero;
    }

    private static TimeSpan? ReadTimeSpan(ExpressionSyntax expression)
    {
        if (expression is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.Text: "TimeSpan" }
                } factory
            } call)
            return null;

        var argument = call.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (argument is null || ReadNumber(argument) is not { } value) return null;

        return factory.Name.Identifier.Text switch
        {
            "FromSeconds" => TimeSpan.FromSeconds(value),
            "FromMilliseconds" => TimeSpan.FromMilliseconds(value),
            _ => null
        };
    }

    private static double? ReadNumber(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.NumericLiteralExpression)
            ? Convert.ToDouble(literal.Token.Value)
            : null;

    private static bool IsSleepOrDelay(InvocationExpressionSyntax call) =>
        call.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Sleep" or "Delay" };
}
