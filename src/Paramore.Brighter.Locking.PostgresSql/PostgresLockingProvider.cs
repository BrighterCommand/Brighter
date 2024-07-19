#nullable enable

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.Locking.PostgresSql;

public class PostgresLockingProvider(PostgresLockingProviderOptions options) : IDistributedLock
{
    private readonly ConcurrentDictionary<string, NpgsqlConnection> _connections = new();

    /// <inheritdoc />
    public async Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        if (_connections.ContainsKey(resource))
        {
            return null;
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
            return null;
        }

        _connections.TryAdd(resource, connection);
        return resource;
    }

    /// <inheritdoc />
    public async Task ReleaseLockAsync(
        string resource,
        string lockId,
        CancellationToken cancellationToken
    )
    {
        if (!_connections.TryRemove(resource, out var connection))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(@RESOURCE_HASH_CODE, @RESOURCE_LEN)";
        command.Parameters.AddWithValue("@RESOURCE_HASH_CODE", resource.GetHashCode());
        command.Parameters.AddWithValue("@RESOURCE_LEN", resource.Length);
        await command.ExecuteScalarAsync(cancellationToken);

        await connection.CloseAsync();
        await connection.DisposeAsync();
    }
}
