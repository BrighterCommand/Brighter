using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class When_a_third_party_circuit_breaker_reports_tripped_topics_should_report_them_tests
{
    [Fact]
    public async Task When_a_third_party_circuit_breaker_reports_tripped_topics_should_report_them()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        var breaker = new ListedTopicsCircuitBreaker("orders", "payments");
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.True(state.Read().IsActive);
        Assert.Equal(HealthStatus.Degraded, result.Status);
        var tripped = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["tripped_topics"]);
        Assert.Equal(new[] { "orders", "payments" }, Assert.IsType<string[]>(tripped["topics"]));
        Assert.Equal(2, tripped["count"]);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
    }
}
