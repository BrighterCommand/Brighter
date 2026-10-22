using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class InvestigateCheckNoSweeperStateTests
{
    [Fact]
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
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("no outbox sweeper is registered", result.Description, System.StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal("investigate", result.Data[RemedyCheckConvention.RemedyKindKey]);
        var conditions = Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]);
        Assert.Empty(conditions);
        Assert.Equal(0, lookups);
    }
}
