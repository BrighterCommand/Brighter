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

using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.GeneratorPlan;

/// <summary>
/// <c>Plan</c> exists so that a caller can ask what the generator would write without writing it -
/// which is only worth anything if asking leaves the configuration as it found it. The prefix the
/// templates read is not always the one the configuration file declares, and the obvious way to
/// reconcile that is to edit the configuration on the way past. Then planning becomes a mutation,
/// planning twice disagrees with planning once, and the caller the new API invites - plan, inspect,
/// then generate - gets a different answer than the one they inspected.
/// </summary>
public class GenerationPlanPurityTests
{
    private static TestConfiguration MultipleGatewaysAndOutboxes() => new()
    {
        Namespace = "Sample.Tests",
        DestinationFolder = "/sample",
        MessagingGateways = new() { ["Sample"] = new MessagingGatewayConfiguration() },
        Outboxes = new() { ["Sample"] = new OutboxConfiguration() },
    };

    [Fact]
    public void When_planning_twice_should_not_change_the_configuration()
    {
        // Arrange — the multiple-gateway and multiple-outbox forms, whose model prefix is
        // dot-qualified while their destination folder name is not
        var configuration = MultipleGatewaysAndOutboxes();

        // Act
        new Generators.MessagingGatewayGenerator(
            NullLogger<Generators.MessagingGatewayGenerator>.Instance).Plan(configuration);
        new Generators.OutboxGenerator(
            NullLogger<Generators.OutboxGenerator>.Instance).Plan(configuration);

        // Assert — the caller's own configuration objects still say what the file said
        Assert.Equal(string.Empty, configuration.MessagingGateways!["Sample"].Prefix);
        Assert.Equal(string.Empty, configuration.Outboxes!["Sample"].Prefix);
    }

    [Fact]
    public void When_planning_twice_should_produce_the_same_files_both_times()
    {
        // Arrange
        var configuration = MultipleGatewaysAndOutboxes();
        var gatewayGenerator = new Generators.MessagingGatewayGenerator(
            NullLogger<Generators.MessagingGatewayGenerator>.Instance);

        // Act
        var first = gatewayGenerator.Plan(configuration).Select(file => file.DestinationPath).ToArray();
        var second = gatewayGenerator.Plan(configuration).Select(file => file.DestinationPath).ToArray();

        // Assert — and non-vacuously: a plan of nothing would satisfy equality trivially
        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }
}
