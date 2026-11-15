using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class RestartCheckNoSweeperStateTests
{
    [Test]
    public async Task When_the_restart_check_has_no_sweeper_state_should_report_healthy_with_no_sweeper_registered()
    {
        //Arrange
        var check = new OutboxSweeperRestartHealthCheck(null, null);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).Contains("no outbox sweeper is registered");
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Restart));
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo("restart");
        var conditions = await Assert.That(result.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>();
        await Assert.That(conditions).IsEmpty();
    }
}
