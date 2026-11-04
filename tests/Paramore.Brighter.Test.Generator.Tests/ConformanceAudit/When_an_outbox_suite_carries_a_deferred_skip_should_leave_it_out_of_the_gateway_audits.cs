using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// The gateway Skip audits cover the messaging-gateway artifacts only. A generated outbox suite
/// carries its own deferred marks, so its files must stay outside the set they walk.
/// </summary>
public class GatewayArtifactScopeAuditTests
{
    [Fact]
    public void When_an_outbox_suite_carries_a_deferred_skip_should_leave_it_out_of_the_gateway_audits()
    {
        // Arrange
        var repoRoot = Path.Combine(Path.GetTempPath(), $"gateway-scope-{Guid.NewGuid():N}");
        var templateDir = Path.Combine(
            repoRoot, "tools", "Paramore.Brighter.Test.Generator", "Templates", "MessagingGateway");
        var testProject = Path.Combine(repoRoot, "tests", "Paramore.Brighter.Scope.Tests");
        var gatewayDir = Path.Combine(testProject, "MessagingGateway", "Generated", "Reactor");
        var outboxDir = Path.Combine(testProject, "Outbox", "Generated", "OutstandingCount");

        Directory.CreateDirectory(templateDir);
        Directory.CreateDirectory(gatewayDir);
        Directory.CreateDirectory(outboxDir);

        try
        {
            var template = Path.Combine(templateDir, "gateway.cs.liquid");
            var gatewayFile = Path.Combine(gatewayDir, "When_a_gateway_test_is_generated.cs");
            var outboxFile = Path.Combine(outboxDir, "When_an_outbox_test_is_generated.cs");
            File.WriteAllText(template, "[Fact(Skip = \"Deferred: #4240\")]\n");
            File.WriteAllText(gatewayFile, "[Fact(Skip = \"Deferred: #4240\")]\n");
            File.WriteAllText(outboxFile, "[Fact(Skip = \"Deferred: #9999\")]\n");

            // Act
            var artifacts = GatewaySkipConventionAudit.EnumerateGatewayArtifacts(repoRoot).ToList();

            // Assert
            Assert.Contains(template, artifacts);
            Assert.Contains(gatewayFile, artifacts);
            Assert.DoesNotContain(outboxFile, artifacts);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }
}
