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
/// Finds the messages a generated test sends, so a test can check each one gets its own id and body
/// without counting how many times the builder call is written.
/// </summary>
internal static class GeneratedSentMessages
{
    public static IReadOnlyList<SentMessage> In(string generatedSource)
    {
        var root = CSharpSyntaxTree.ParseText(generatedSource).GetRoot();
        var sent = new List<SentMessage>();

        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsSend(call)) continue;
            if (call.ArgumentList.Arguments.FirstOrDefault()?.Expression is not IdentifierNameSyntax message) continue;

            var built = HowBuilt(call, message.Identifier.Text);
            sent.Add(new SentMessage(
                message.Identifier.Text,
                HasOwnId: CallsWithRandomId(built, "SetMessageId"),
                HasOwnBody: CallsWithRandomId(built, "SetBody")));
        }

        return sent;
    }

    private static bool IsSend(InvocationExpressionSyntax call) =>
        call.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Send" or "SendAsync" };

    private static SyntaxNode? HowBuilt(SyntaxNode send, string messageName)
    {
        // the builder chain the message variable was declared with, in the same test method
        var test = send.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        return test?.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(v => v.Identifier.Text == messageName)?.Initializer?.Value;
    }

    private static bool CallsWithRandomId(SyntaxNode? built, string builderMethod)
    {
        if (built is null) return false;

        return built.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(call =>
            call.Expression is MemberAccessExpressionSyntax access
            && access.Name.Identifier.Text == builderMethod
            && call.ArgumentList.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsIdRandom));
    }

    private static bool IsIdRandom(InvocationExpressionSyntax call) =>
        call.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.Text: "Id" },
            Name.Identifier.Text: "Random"
        };
}
