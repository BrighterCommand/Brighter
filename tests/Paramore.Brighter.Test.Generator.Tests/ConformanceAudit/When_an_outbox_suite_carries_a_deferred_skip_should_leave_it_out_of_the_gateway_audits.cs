using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// The gateway Skip audits cover the messaging-gateway artifacts only. A generated outbox suite
/// carries its own deferred marks, so its files must stay outside the set they walk.
/// </summary>
public class GatewayArtifactScopeAuditTests
{
    [Test]
    public async Task When_an_outbox_suite_carries_a_deferred_skip_should_leave_it_out_of_the_gateway_audits()
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
            File.WriteAllText(template, "[Test]\n[Skip(\"Deferred: #4240\")]\n");
            File.WriteAllText(gatewayFile, "[Test]\n[Skip(\"Deferred: #4240\")]\n");
            File.WriteAllText(outboxFile, "[Test]\n[Skip(\"Deferred: #9999\")]\n");

            // Act
            var artifacts = GatewaySkipConventionAudit.EnumerateGatewayArtifacts(repoRoot).ToList();

            // Assert
            await Assert.That(artifacts).Contains(template);
            await Assert.That(artifacts).Contains(gatewayFile);
            await Assert.That(artifacts).DoesNotContain(outboxFile);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }
}
