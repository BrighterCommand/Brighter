using System.Collections.Concurrent;
using System.Data;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Oracle;

namespace Paramore.Brighter.Locking.Oracle;

/// <summary>
/// Provides Oracle-backed distributed locking using <c>DBMS_LOCK</c> APIs.
/// </summary>
/// <param name="connectionProvider">Provides Oracle connections used to allocate, request, and release locks.</param>
public partial class OracleLockingProvider(OracleConnectionProvider connectionProvider) : IDistributedLock, IAsyncDisposable
{
    private readonly ILogger _logger = ApplicationLogging.CreateLogger<OracleLockingProvider>();
    private readonly ConcurrentDictionary<string, OracleConnection> _connections = new();


#if NETFRAMEWORK
    /// <summary>
    /// Releases all held Oracle connections tracked by this lock provider.
    /// </summary>
    /// <returns>A completed <see cref="ValueTask"/> once all connections are released.</returns>
    public ValueTask DisposeAsync()
    {
        foreach (var connection in _connections.Values)
        {
            connection.Close();
            connection.Dispose();
        }

        _connections.Clear();
        return new ValueTask();
    }
#else
    /// <summary>
    /// Releases all held Oracle connections tracked by this lock provider.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when all connections are asynchronously released.</returns>
    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections.Values)
        {
            await connection.CloseAsync();
            await connection.DisposeAsync();
        }

        _connections.Clear();
    }
#endif

    /// <summary>
    /// Attempts to obtain an exclusive lock for the specified resource.
    /// </summary>
    /// <param name="resource">The resource identifier to lock.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing the Oracle lock handle when the lock is acquired; otherwise <see langword="null"/>.
    /// </returns>
    public async Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        if (_connections.ContainsKey(resource))
        {
            Log.LockAlreadyHeld(_logger, resource);
            return null;
        }

        string? lockHandler = null;

        var connection = (OracleConnection)await connectionProvider.GetConnectionAsync(cancellationToken);
        try
        {
            lockHandler = await AllocateLockHandleAsync(connection, resource);
            if (string.IsNullOrEmpty(lockHandler))
            {
                Log.LockHandleAllocationFailed(_logger, resource);
                return null;
            }

            var requestLock = await RequestLock(connection, lockHandler, 1);
            if (requestLock > 0)
            {
                Log.LockNotGranted(_logger, resource, requestLock);
                return null;
            }

            _connections.TryAdd(resource, connection);
            Log.LockObtained(_logger, resource, lockHandler!);
            return lockHandler;
        }
        catch (Exception e)
        {
            Log.LockAcquisitionFailed(_logger, e, resource);
            if (!string.IsNullOrEmpty(lockHandler))
            {
                await ReleaseLockHandlerAsync(connection, lockHandler!);
            }
#if NETFRAMEWORK
            connection.Close();
            connection.Dispose();
#else
            await connection.CloseAsync();
            await connection.DisposeAsync();
#endif
            return null;
        }
    }

    /// <summary>
    /// Releases a previously obtained lock for the specified resource.
    /// </summary>
    /// <param name="resource">The resource identifier that is currently locked.</param>
    /// <param name="lockId">The lock identifier returned by <see cref="ObtainLockAsync"/>.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="Task"/> that completes when the lock release operation has finished.</returns>
    public async Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken)
    {
        if (!_connections.TryRemove(resource, out var connection))
        {
            Log.LockReleaseSkipped(_logger, resource, lockId);
            return;
        }

        await ReleaseLockHandlerAsync(connection, lockId);
        Log.LockReleased(_logger, resource, lockId);

#if NETFRAMEWORK
        connection.Close();
        connection.Dispose();
#else
        await connection.CloseAsync();
        await connection.DisposeAsync();
#endif
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information, "Unable to obtain lock for resource {Resource}; a connection is already tracking this lock")]
        public static partial void LockAlreadyHeld(ILogger logger, string resource);

        [LoggerMessage(LogLevel.Information, "Unable to obtain lock for resource {Resource}; Oracle DBMS_LOCK returned status {ResultCode}")]
        public static partial void LockNotGranted(ILogger logger, string resource, int resultCode);

        [LoggerMessage(LogLevel.Warning, "Unable to obtain lock for resource {Resource}; Oracle DBMS_LOCK did not return a lock handle")]
        public static partial void LockHandleAllocationFailed(ILogger logger, string resource);

        [LoggerMessage(LogLevel.Information, "Obtained lock for resource {Resource} with handle {LockHandle}")]
        public static partial void LockObtained(ILogger logger, string resource, string lockHandle);

        [LoggerMessage(LogLevel.Error, "Failed to obtain lock for resource {Resource}")]
        public static partial void LockAcquisitionFailed(ILogger logger, Exception exception, string resource);

        [LoggerMessage(LogLevel.Debug, "Skipping release for resource {Resource}; no tracked connection for lock {LockId}")]
        public static partial void LockReleaseSkipped(ILogger logger, string resource, string lockId);

        [LoggerMessage(LogLevel.Information, "Released lock {LockId} for resource {Resource}")]
        public static partial void LockReleased(ILogger logger, string resource, string lockId);
    }

    private static async Task<string?> AllocateLockHandleAsync(OracleConnection conn, string lockName)
    {
        const string plsql = """
                             BEGIN 
                                 DBMS_LOCK.ALLOCATE_UNIQUE(:name, :handle); 
                             END;
                             """;

#if NETFRAMEWORK
        using var cmd = new OracleCommand(plsql, conn);
#else
        await using var cmd = new OracleCommand(plsql, conn);
#endif

        var handle = new OracleParameter("handle", OracleDbType.Varchar2, 128)
        {
            Direction = ParameterDirection.Output
        };

        cmd.Parameters.Add(new OracleParameter("name", OracleDbType.Varchar2)
        {
            Value = lockName,
            Direction = ParameterDirection.Input
        });
        cmd.Parameters.Add(handle);
        await cmd.ExecuteNonQueryAsync();

        return handle.Value?.ToString();
    }


    private static async Task<int> RequestLock(OracleConnection conn, string? lockHandle, int timeoutSeconds)
    {
        // 6 = X_MODE (Exclusive Mode). release_on_commit = FALSE keeps lock alive across app updates.
        const string plsql = """
                             BEGIN 
                                :result := DBMS_LOCK.REQUEST(:lockhandle, 6, :timeout, FALSE); 
                             END;
                             """;

#if NETFRAMEWORK
        using var cmd = new OracleCommand(plsql, conn);
#else
        await using var cmd = new OracleCommand(plsql, conn);
#endif
        var resultParam = new OracleParameter("result", OracleDbType.Int32, ParameterDirection.Output);

        cmd.Parameters.Add(resultParam);
        cmd.Parameters.Add("lockhandle", OracleDbType.Varchar2, lockHandle, ParameterDirection.Input);
        cmd.Parameters.Add("timeout", OracleDbType.Int32, timeoutSeconds, ParameterDirection.Input);

        await cmd.ExecuteNonQueryAsync();
        return Convert.ToInt32(resultParam.Value?.ToString());
    }

    private static async Task ReleaseLockHandlerAsync(OracleConnection conn, string lockHandle)
    {
        const string plsql = """
                             BEGIN 
                                :result := DBMS_LOCK.RELEASE(:lockhandle); 
                             END;
                             """;

#if NETFRAMEWORK
        using var cmd = new OracleCommand(plsql, conn);
#else
        await using var cmd = new OracleCommand(plsql, conn);
#endif
        var resultParam = new OracleParameter("result", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add(resultParam);
        cmd.Parameters.Add("lockhandle", OracleDbType.Varchar2, lockHandle, ParameterDirection.Input);

        await cmd.ExecuteNonQueryAsync();
    }
}
