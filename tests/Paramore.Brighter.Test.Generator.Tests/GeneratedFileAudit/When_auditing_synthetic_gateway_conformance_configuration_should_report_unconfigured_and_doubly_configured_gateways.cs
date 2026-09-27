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

using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.GeneratedFileAudit;

/// <summary>
/// <see cref="GatewayConformanceAudit"/> over synthetic inputs, so the failure cases can be
/// asserted at all - the real tree has neither after every gateway got a configuration.
/// </summary>
public class SyntheticGatewayConformanceAuditTests
{
    [Fact]
    public void When_auditing_synthetic_gateway_conformance_configuration_should_report_unconfigured_and_doubly_configured_gateways()
    {
        // Arrange - gateway A is configured once, B not at all, C twice
        var gatewayNamespaces = new[] { "A", "B", "C" };
        var configuredSubjects = new[]
        {
            new GatewayConfigurationEntry("A.Foo"),
            new GatewayConfigurationEntry("C.Foo"),
            new GatewayConfigurationEntry("C.Bar")
        };

        // Act
        var audit = GatewayConformanceAudit.Of(gatewayNamespaces, configuredSubjects);

        // Assert
        Assert.Equal(new[] { "B" }, audit.Unconfigured);
        Assert.Equal(new[] { "C" }, audit.ConfiguredMoreThanOnce);
    }

    [Fact]
    public void When_two_gateway_namespaces_share_a_prefix_should_not_conflate_them()
    {
        // Arrange - X and X.V2 are each configured once, with a type in their own namespace. Under
        // a prefix comparison (StartsWith), X.V2's configuration would also name X, making X
        // "named by two" - the comparison must be exact equality of the containing namespace
        var gatewayNamespaces = new[] { "X", "X.V2" };
        var configuredSubjects = new[]
        {
            new GatewayConfigurationEntry("X.Foo"),
            new GatewayConfigurationEntry("X.V2.Bar")
        };

        // Act
        var audit = GatewayConformanceAudit.Of(gatewayNamespaces, configuredSubjects);

        // Assert
        Assert.Empty(audit.Unconfigured);
        Assert.Empty(audit.ConfiguredMoreThanOnce);
    }

    [Fact]
    public void When_an_additional_expected_subject_lands_in_an_already_configured_gateways_namespace_should_not_count_as_naming_it()
    {
        // Arrange - gateway A is configured once via SubscriptionType, and its
        // AdditionalExpectedSubjects names a second type in the same namespace (the case that key
        // exists for: a second declaring type in an existing gateway). If AdditionalExpectedSubjects
        // counted as naming, A would be reported as configured twice
        var gatewayNamespaces = new[] { "A" };
        var configuredSubjects = new[]
        {
            new GatewayConfigurationEntry("A.Foo", new[] { "A.AnotherDeclaringType" })
        };

        // Act
        var audit = GatewayConformanceAudit.Of(gatewayNamespaces, configuredSubjects);

        // Assert
        Assert.Empty(audit.Unconfigured);
        Assert.Empty(audit.ConfiguredMoreThanOnce);
    }
}
