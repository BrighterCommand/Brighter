using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.PostgreSql;
using Npgsql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public class PostgreSqlConcurrentFirstTripAnnouncementTests : IAsyncLifetime
{
    private const int Repetitions = 1000;
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan s_laterOffset = TimeSpan.FromSeconds(5);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = s_cooldown };

    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_trips_{Guid.NewGuid():N}";
    private readonly string _run = Guid.NewGuid().ToString("N");
    private readonly List<Activity> _activities = [];

    [Fact]
    public async Task When_two_breakers_trip_a_topic_concurrently_against_the_database_should_announce_one_trip_and_one_retrip()
    {
        // Arrange
        await ProvisionTripTableAsync();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("postgresql-concurrent-first-trip-tracer"))
            .AddInMemoryExporter(_activities)
            .Build();
        var tracer = new BrighterTracer();
        var breakerA = NewBreaker(tracer, s_start);
        var breakerB = NewBreaker(tracer, s_start + s_laterOffset);
        var observer = NewStore();
        var topics = Enumerable.Range(0, Repetitions).Select(i => new RoutingKey($"orders-{_run}-{i}")).ToList();
        var later = s_start + s_laterOffset;

        // Act
        foreach (var topic in topics)
        {
            var batchA = breakerA.BeginTripBatch();
            var batchB = breakerB.BeginTripBatch();
            batchA.TripTopic(topic);
            batchB.TripTopic(topic);
            await Task.WhenAll(Task.Run(() => batchA.WriteAsync()), Task.Run(() => batchB.WriteAsync()));
        }

        var liveBeforeEnd = await observer.ReadLiveAsync(new TripWindow(later + s_cooldown - TimeSpan.FromSeconds(0.1), s_cooldown));
        var endedBeforeEnd = await observer.EndExpiredAsync(new TripWindow(later + s_cooldown - TimeSpan.FromSeconds(0.1), s_cooldown));
        var endedAtEnd = await observer.EndExpiredAsync(new TripWindow(later + s_cooldown, s_cooldown));
        var liveAtEnd = await observer.ReadLiveAsync(new TripWindow(later + s_cooldown, s_cooldown));
        provider.ForceFlush();
        string[] spanNames;
        lock (_activities)
            spanNames = _activities.Select(a => a.DisplayName).ToArray();

        // Assert
        Assert.Equal(topics.Select(t => t.Value).Order(), liveBeforeEnd.Select(t => t.Topic.Value).Order());
        Assert.All(liveBeforeEnd, trip => Assert.Equal(later, trip.TripTime));
        Assert.Empty(endedBeforeEnd);
        Assert.Empty(liveAtEnd);
        Assert.Equal(Repetitions, endedAtEnd.Count);
        foreach (var topic in topics)
        {
            Assert.Equal(1, spanNames.Count(n => n == $"{topic} {CircuitBreakerSpanOperation.Trip.ToSpanName()}"));
            Assert.Equal(1, spanNames.Count(n => n == $"{topic} {CircuitBreakerSpanOperation.ReTrip.ToSpanName()}"));
        }
        Assert.Equal(Repetitions * 2, spanNames.Count(n => n.Contains(_run)));
    }

    private IAmATrippedTopicStore NewStore() =>
        new PostgreSqlTrippedTopicStore(new PostgreSqlConnectionProvider(new RelationalDatabaseConfiguration(_connectionString)), _tableName);

    private DistributedOutboxCircuitBreaker NewBreaker(BrighterTracer tracer, DateTimeOffset clockStart) =>
        new(NewStore(), s_options, tracer, timeProvider: new FakeTimeProvider(clockStart));

    private async Task ProvisionTripTableAsync()
    {
        new PostgresSqlTestHelper().SetupDatabase();
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new RelationalDatabaseConfiguration(_connectionString);
        new StubBrighterBuilder(services).UseBoxProvisioning(options =>
            options.AddPostgreSqlTrippedTopics(configuration, _tableName));

        await services.BuildServiceProvider().GetServices<IAmABoxProvisioner>().Single().ProvisionAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS \"public\".\"{_tableName}\"";
        await command.ExecuteNonQueryAsync();
    }
}
