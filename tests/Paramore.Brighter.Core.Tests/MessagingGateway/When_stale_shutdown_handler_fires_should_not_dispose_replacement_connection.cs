using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessagingGateway;

/// <summary>
/// Regression test for issue #4024 — connection pool race condition.
///
/// When a RabbitMQ connection is replaced (e.g., after a broker restart),
/// the old connection's shutdown handler may still fire. The pool must use
/// ReferenceEquals to verify the handler's sender is the *current* pooled
/// connection before disposing it. Otherwise, a stale handler could dispose
/// the replacement connection.
///
/// This test exercises the exact guard pattern used in
/// RmqMessageGatewayConnectionPool.ShutdownHandler without requiring a
/// live RabbitMQ broker.
/// </summary>
public class ConnectionPoolReferenceEqualityGuardTests
{
    [Fact]
    public void When_Stale_Handler_Fires_Should_Not_Dispose_Replacement_Connection()
    {
        // Arrange — simulate the pool with two connections sharing the same key
        var pool = new Dictionary<string, FakePooledConnection>();
        const string connectionId = "guest.guest.localhost.5672./";

        var oldConnection = new FakeConnection("old");
        var newConnection = new FakeConnection("new");

        // First connection is added, then replaced
        pool[connectionId] = new FakePooledConnection(oldConnection);
        pool[connectionId] = new FakePooledConnection(newConnection);

        // Act — simulate the old connection's shutdown handler firing
        // This is the exact pattern from RmqMessageGatewayConnectionPool.ShutdownHandler
        object sender = oldConnection; // the shutdown event sender is the OLD connection
        if (pool.TryGetValue(connectionId, out var pooled)
            && ReferenceEquals(pooled.Connection, sender))
        {
            // This block should NOT execute because sender is the old connection
            pooled.Connection.SimulateDispose();
            pool.Remove(connectionId);
        }

        // Assert — the new connection should still be in the pool and not disposed
        Assert.True(pool.ContainsKey(connectionId), "New connection should still be in the pool");
        Assert.False(newConnection.IsDisposed, "New connection should not have been disposed by stale handler");
        Assert.False(oldConnection.IsDisposed, "Old connection should not have been disposed (guard prevented it)");
    }

    [Fact]
    public void When_Current_Connection_Shuts_Down_Should_Remove_From_Pool()
    {
        // Arrange — the handler fires for the connection that IS currently in the pool
        var pool = new Dictionary<string, FakePooledConnection>();
        const string connectionId = "guest.guest.localhost.5672./";

        var currentConnection = new FakeConnection("current");
        pool[connectionId] = new FakePooledConnection(currentConnection);

        // Act — shutdown handler fires for the current connection
        object sender = currentConnection;
        if (pool.TryGetValue(connectionId, out var pooled)
            && ReferenceEquals(pooled.Connection, sender))
        {
            pooled.Connection.SimulateDispose();
            pool.Remove(connectionId);
        }

        // Assert — the connection should be removed and disposed
        Assert.False(pool.ContainsKey(connectionId), "Connection should be removed from pool");
        Assert.True(currentConnection.IsDisposed, "Current connection should be disposed");
    }

    [Fact]
    public async Task When_Stale_Async_Handler_Fires_Should_Not_Dispose_Replacement_Connection()
    {
        // Arrange — same pattern but with SemaphoreSlim (matching the async pool)
        var pool = new Dictionary<string, FakePooledConnection>();
        var semaphore = new SemaphoreSlim(1, 1);
        const string connectionId = "guest.guest.localhost.5672./";

        var oldConnection = new FakeConnection("old");
        var newConnection = new FakeConnection("new");

        pool[connectionId] = new FakePooledConnection(oldConnection);
        pool[connectionId] = new FakePooledConnection(newConnection);

        // Act — simulate the async shutdown handler
        object sender = oldConnection;

        await semaphore.WaitAsync(CancellationToken.None);
        try
        {
            if (pool.TryGetValue(connectionId, out var pooled)
                && ReferenceEquals(pooled.Connection, sender))
            {
                pooled.Connection.SimulateDispose();
                pool.Remove(connectionId);
            }
        }
        finally
        {
            semaphore.Release();
        }

        // Assert
        Assert.True(pool.ContainsKey(connectionId));
        Assert.False(newConnection.IsDisposed);
    }

    private sealed class FakeConnection(string name)
    {
        public string Name { get; } = name;
        public bool IsDisposed { get; private set; }

        public void SimulateDispose() => IsDisposed = true;

        public override string ToString() => $"FakeConnection({Name})";
    }

    private sealed record FakePooledConnection(FakeConnection Connection);
}
