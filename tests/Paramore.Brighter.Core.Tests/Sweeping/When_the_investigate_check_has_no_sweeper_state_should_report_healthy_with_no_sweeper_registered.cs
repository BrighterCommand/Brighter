using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class InvestigateCheckNoSweeperStateTests
{
    [Test]
    public async Task When_the_investigate_check_has_no_sweeper_state_should_report_healthy_with_no_sweeper_registered()
    {
        //Arrange
        var lookups = 0;
        var check = new OutboxSweeperInvestigateHealthCheck(null, null, () =>
        {
            lookups++;
            return null;
        });

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).Contains("no outbox sweeper is registered");
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo("investigate");
        var conditions = await Assert.That(result.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>();
        await Assert.That(conditions).IsEmpty();
        await Assert.That(lookups).IsEqualTo(0);
    }
}
