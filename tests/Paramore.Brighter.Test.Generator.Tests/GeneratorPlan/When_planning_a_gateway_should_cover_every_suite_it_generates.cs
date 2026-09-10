#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.GeneratorPlan;

/// <summary>
/// The generator renders three suites for a gateway - Reactor, Proactor and Shared - and the
/// generated-tree audit trusts <c>Plan</c> to name the files of all three.
/// </summary>
/// <remarks>
/// <para>
/// A suite dropped from <c>Plan</c> does not fail loudly. The plan simply owns less, and the audit
/// goes green on a smaller claim - or, where the files are already on disk, reports them as orphans
/// and sends the reader looking for output to delete rather than for a suite to restore. Either way
/// the failure describes a symptom a long way from its cause.
/// </para>
/// <para>
/// The Shared suite is the one at risk, because it is the odd one out: it lands directly in the
/// gateway's <c>Generated</c> folder instead of a variant folder beneath it, and it takes neither
/// the capability-flag ignore nor the ledger's prepareModel. Anything that rewrites how suites are
/// described - the merge that introduced this test among them - is liable to carry the two regular
/// suites across and leave the irregular one behind, so this asks for it by name.
/// </para>
/// </remarks>
public class GenerationPlanCoverageTests
{
    private static TestConfiguration OneGateway() => new()
    {
        Namespace = "Sample.Tests",
        DestinationFolder = "/sample",
        MessagingGateways = new() { ["Sample"] = new MessagingGatewayConfiguration() },
    };

    private static string[] PlannedFolders(TestConfiguration configuration) =>
        new Generators.MessagingGatewayGenerator(
                NullLogger<Generators.MessagingGatewayGenerator>.Instance)
            .Plan(configuration)
            .Select(file => Path.GetDirectoryName(file.DestinationPath)!)
            .Distinct()
            .ToArray();

    [Fact]
    public void When_planning_a_gateway_should_cover_every_suite_it_generates()
    {
        // Arrange
        var configuration = OneGateway();
        var gatewayRoot = Path.Combine("/sample", "MessagingGateway", "Sample", "Generated");

        // Act
        var folders = PlannedFolders(configuration);

        // Assert — all three, named individually, so a missing one says which
        Assert.Contains(Path.Combine(gatewayRoot, "Reactor"), folders);
        Assert.Contains(Path.Combine(gatewayRoot, "Proactor"), folders);
        Assert.Contains(gatewayRoot, folders);
    }

    [Fact]
    public void When_planning_a_gateway_should_plan_the_shared_suites_own_templates()
    {
        // Arrange — the Shared suite's destination folder is a prefix of the other two, so a plan
        // holding only Reactor and Proactor would still put files "under" it. Naming a template
        // only the Shared suite renders is what makes the previous assertion mean the suite ran.
        var configuration = OneGateway();
        var sharedFolder = Path.Combine("/sample", "MessagingGateway", "Sample", "Generated");

        // Act
        var planned = new Generators.MessagingGatewayGenerator(
                NullLogger<Generators.MessagingGatewayGenerator>.Instance)
            .Plan(configuration)
            .Where(file => Path.GetDirectoryName(file.DestinationPath) == sharedFolder)
            .Select(file => Path.GetFileName(file.DestinationPath))
            .ToArray();

        // Assert — non-vacuously: an empty plan would satisfy a subset check trivially
        Assert.NotEmpty(planned);
        Assert.Contains("RejectionMetadataKeys.cs", planned);
    }
}
