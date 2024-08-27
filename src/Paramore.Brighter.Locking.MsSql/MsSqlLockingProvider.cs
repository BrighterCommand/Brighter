#region Licence

/* The MIT License (MIT)
Copyright © 2021 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.Locking.MsSql;

/// <summary>
/// The Microsoft Sql Server Locking Provider
/// </summary>
/// <param name="connectionProvider">The Sql Server connection Provider</param>
public class MsSqlLockingProvider(IMsSqlConnectionProvider connectionProvider) : IDistributedLock, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, DbConnection> _connections = new();

    private readonly ILogger _logger = ApplicationLogging.CreateLogger<MsSqlLockingProvider>();
    /// <summary>
    /// Attempt to obtain a lock on a resource
    /// </summary>
    /// <param name="resource">The name of the resource to Lock</param>
    /// <param name="cancellationToken">The Cancellation Token</param>
    /// <returns>The id of the lock that has been acquired or null if no lock was able to be acquired</returns>
    public async Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        if (_connections.ContainsKey(resource))
        {
            return null;
        }

        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = MsSqlLockingQueries.ObtainLockQuery;
        command.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255));
        command.Parameters["@Resource"].Value = resource;
        command.Parameters.Add(new SqlParameter("@LockTimeout", SqlDbType.Int));
        command.Parameters["@LockTimeout"].Value = 0;

        var result = (await command.ExecuteScalarAsync(cancellationToken)) ?? -999;

        var resultCode = (int)result;

        _logger.LogInformation("Attempt to obtain lock returned: {MsSqlLockResult}", GetLockStatusCode(resultCode));
        
        if (resultCode < 0)
            return null;

        _connections.TryAdd(resource, connection);
        
        return resource;
    }

    /// <summary>
    /// Release a lock
    /// </summary>
    /// <param name="resource">The name of the resource to Lock</param>
    /// <param name="lockId">The lock Id that was provided when the lock was obtained</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Awaitable Task</returns>
    public async Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken)
    {
        if (!_connections.TryRemove(resource, out var connection))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = MsSqlLockingQueries.ReleaseLockQuery;
        command.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255));
        command.Parameters["@Resource"].Value = resource;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await connection.CloseAsync();
        await connection.DisposeAsync();
    }

    /// <summary>
    /// Convert Status code to messages
    /// Doc: https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql?view=sql-server-ver16#return-code-values
    /// </summary>
    /// <param name="code">Status Code</param>
    /// <returns>Status Message</returns>
    private string GetLockStatusCode(int code)
        => code switch
        {
            0 => "The lock was successfully granted synchronously.",
            1 => "The lock was granted successfully after waiting for other incompatible locks to be released.",
            -1 => "The lock request timed out.",
            -2 => "The lock request was canceled.",
            -3 => "The lock request was chosen as a deadlock victim.",
            _ => "Indicates a parameter validation or other call error."
        };

    /// <summary>
    /// Dispose Locking Provider
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.Value.DisposeAsync();
        }
    }
}
