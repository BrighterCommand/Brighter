using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.Locking.PostgresSql;

public class PostgresLockingProvider(PostgresLockingProviderOptions options) : IDistributedLock
{
    private readonly ConcurrentDictionary<string, NpgsqlConnection> _connections = new();

    /// <inheritdoc />
    public async Task<bool> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        if (_connections.ContainsKey(resource))
        {
            return false;
        }

        var connection = new NpgsqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@RESOURCE_HASH_CODE, @RESOURCE_LEN)";
        command.Parameters.AddWithValue("@RESOURCE_HASH_CODE", resource.GetHashCode());
        command.Parameters.AddWithValue("@RESOURCE_LEN", resource.Length);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        if (scalar is not (null or true))
        {
            return false;
        }

        _connections.TryAdd(resource, connection);
        return true;
    }

    /// <inheritdoc />
    public bool ObtainLock(string resource)
    {
        if (_connections.ContainsKey(resource))
        {
            return false;
        }

        var connection = new NpgsqlConnection(options.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@RESOURCE_HASH_CODE, @RESOURCE_LEN)";
        command.Parameters.AddWithValue("@RESOURCE_HASH_CODE", resource.GetHashCode());
        command.Parameters.AddWithValue("@RESOURCE_LEN", resource.Length);
        var scalar = command.ExecuteScalar();
        if (scalar is not (null or true))
        {
            return false;
        }

        _connections.TryAdd(resource, connection);
        return true;
    }

    /// <inheritdoc />
    public async Task ReleaseLockAsync(string resource, CancellationToken cancellationToken)
    {
        if (!_connections.TryRemove(resource, out var connection))
        {
            return;
        }

        await connection.CloseAsync();
        await connection.DisposeAsync();
    }

    /// <inheritdoc />
    public void ReleaseLock(string resource)
    {
        if (!_connections.TryRemove(resource, out var connection))
        {
            return;
        }

        connection.Close();
        connection.Dispose();
    }
}
