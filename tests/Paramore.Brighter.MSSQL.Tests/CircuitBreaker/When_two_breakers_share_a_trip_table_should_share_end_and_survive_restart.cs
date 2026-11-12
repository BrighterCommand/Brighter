using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.CircuitBreaker;

public class MsSqlSharedTripTableTests : IAsyncLifetime
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly RoutingKey s_orders = new("orders");

    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"shared_trips_{Guid.NewGuid():N}";
    private readonly List<Activity> _activities = [];

    [Fact]
    public async Task When_two_breakers_share_a_trip_table_should_share_end_and_survive_restart()
    {
        // Arrange
        await ProvisionTripTableAsync();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("mssql-shared-trips-tracer"))
            .AddInMemoryExporter(_activities)
            .Build();
        var clock = new FakeTimeProvider(s_start);
        var breakerA = NewBreaker(clock);
        var breakerB = NewBreaker(clock);

        // Act
        var batch = breakerA.BeginTripBatch();
        batch.TripTopic(s_orders);
        await batch.WriteAsync();

        clock.Advance(TimeSpan.FromSeconds(10));
        var seenByB = await breakerB.ReadTrippedTopicsAsync();
        var recreated = NewBreaker(clock);
        var seenByRecreated = await recreated.ReadTrippedTopicsAsync();

        clock.Advance(TimeSpan.FromSeconds(40));
        var afterCooldownByB = await breakerB.ReadTrippedTopicsAsync();
        var afterCooldownByA = await breakerA.ReadTrippedTopicsAsync();
        var afterCooldownByRecreated = await recreated.ReadTrippedTopicsAsync();
        provider.ForceFlush();

        // Assert
        Assert.Contains(s_orders, seenByB);
        Assert.Contains(s_orders, seenByRecreated);
        Assert.DoesNotContain(s_orders, afterCooldownByB);
        Assert.DoesNotContain(s_orders, afterCooldownByA);
        Assert.DoesNotContain(s_orders, afterCooldownByRecreated);
        Assert.Equal(1, SpanCount("orders reset"));
    }

    private DistributedOutboxCircuitBreaker NewBreaker(TimeProvider clock)
    {
        var connectionProvider = new MsSqlConnectionProvider(new RelationalDatabaseConfiguration(_connectionString));
        var store = new MsSqlTrippedTopicStore(connectionProvider, _tableName);
        return new DistributedOutboxCircuitBreaker(store, tracer: new BrighterTracer(), timeProvider: clock);
    }

    private async Task ProvisionTripTableAsync()
    {
        Configuration.EnsureDatabaseExists(_connectionString);
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new RelationalDatabaseConfiguration(_connectionString);
        new StubBrighterBuilder(services).UseBoxProvisioning(options =>
            options.AddMsSqlTrippedTopics(configuration, _tableName));

        await services.BuildServiceProvider().GetServices<IAmABoxProvisioner>().Single().ProvisionAsync();
    }

    private int SpanCount(string name)
    {
        lock (_activities)
            return _activities.Count(a => a.DisplayName == name);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS [dbo].[{_tableName}]";
        await command.ExecuteNonQueryAsync();
    }
}
