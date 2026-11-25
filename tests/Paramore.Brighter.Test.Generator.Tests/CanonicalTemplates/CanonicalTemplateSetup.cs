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
using System.IO;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// The Arrange the canonical template tests share: a ledger that passes one cell, the gateway
/// configuration they generate from, and where each variant's generated file lands.
/// </summary>
internal static class CanonicalTemplateSetup
{
    public const string REACTOR = "Reactor";
    public const string PROACTOR = "Proactor";

    public static InMemoryConformanceLedger PassLedger(string ledgerKey, string frColumn) =>
        new(new Dictionary<(string, string), string>
        {
            [(ledgerKey, frColumn)] = "Pass"
        });

    public static TestConfiguration BuildConfiguration(string destinationFolder, string ledgerKey) =>
        new()
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = destinationFolder,
            MessageBuilder = "TestMessageBuilder",
            MessageAssertion = "TestMessageAssertion",
            MessagingGateway = new MessagingGatewayConfiguration
            {
                Prefix = "Test",
                Namespace = "MyApp.Tests",
                MessageGatewayProvider = "TestProvider",
                Publication = "Publication",
                Subscription = "Subscription",
                LedgerKey = ledgerKey,
            }
        };

    public static string OutputPath(string destinationFolder, string variant, string templateName) =>
        Path.Combine(
            destinationFolder, "MessagingGateway", "Test", "Generated", variant,
            $"{templateName}.cs");
}
