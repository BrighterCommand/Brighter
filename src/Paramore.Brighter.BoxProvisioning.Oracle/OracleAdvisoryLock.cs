// The MIT License (MIT)
// Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace Paramore.Brighter.BoxProvisioning.Oracle;

/// <summary>
/// Default <see cref="IOracleAdvisoryLock"/> backed by Oracle <c>DBMS_LOCK.ALLOCATE_UNIQUE</c>,
/// <c>DBMS_LOCK.REQUEST</c>, and <c>DBMS_LOCK.RELEASE</c>.
/// </summary>
/// <remarks>
/// <para>
/// The connecting user must hold <c>EXECUTE ON DBMS_LOCK</c> (or
/// <c>EXECUTE ANY PROCEDURE</c>). Both <c>ALLOCATE_UNIQUE</c> and <c>REQUEST</c> are called
/// in the same session so the allocated handle is stable for the lock's lifetime.
/// </para>
/// <para>
/// Because <c>ALLOCATE_UNIQUE</c> is idempotent for the same lock name within the same
/// database, <c>ReleaseAsync</c> re-allocates the handle for the given <paramref name="lockKey"/>
/// before releasing — this keeps the interface stateless and avoids storing per-session state
/// on the instance.
/// </para>
/// <para>
/// <c>DBMS_LOCK.REQUEST</c> return codes:
/// 0 = success, 1 = timeout, 2 = deadlock, 3 = parameter error, 4 = already held (same
/// session), 5 = illegal lock handle.
/// </para>
/// </remarks>
public class OracleAdvisoryLock : IOracleAdvisoryLock
{
    /// <inheritdoc />
    public async Task AcquireAsync(
        OracleConnection connection, string lockKey,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout,
                "Migration lock timeout must be non-negative.");
        }

        var timeoutSeconds = (int)Math.Ceiling(timeout.TotalSeconds);
        var lockHandle = await AllocateLockHandleAsync(connection, lockKey, cancellationToken);
        if (string.IsNullOrEmpty(lockHandle))
        {
            throw new OracleAdvisoryLockException($"DBMS_LOCK.ALLOCATE_UNIQUE returned an empty handle for '{lockKey}'.");
        }

        var result = await RequestLockAsync(connection, lockHandle!, timeoutSeconds, cancellationToken);

        switch (result)
        {
            case 0:
            case 4:
                return;
            case 1: throw new TimeoutException(
                $"Could not acquire migration lock '{lockKey}' within {timeoutSeconds}s.");
            case 2: throw new OracleAdvisoryLockException(
                $"DBMS_LOCK.REQUEST on '{lockKey}' detected a deadlock (return code 2).");
            default: throw new OracleAdvisoryLockException(
                $"DBMS_LOCK.REQUEST on '{lockKey}' failed with return code {result}.");
        }
    }

    /// <inheritdoc />
    public async Task<bool?> ReleaseAsync(
        OracleConnection connection, string lockKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var lockHandle = await AllocateLockHandleAsync(connection, lockKey, cancellationToken);
            if (string.IsNullOrEmpty(lockHandle))
            {
                return null;
            }
            
            var result = await ReleaseLockByHandleAsync(connection, lockHandle!, cancellationToken);
            return result == 0;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> AllocateLockHandleAsync(
        OracleConnection connection, string lockKey,
        CancellationToken cancellationToken)
    {
        const string plsql = """
                             BEGIN
                                 DBMS_LOCK.ALLOCATE_UNIQUE(:name, :handle);
                             END;
                             """;

#if NETFRAMEWORK
        using var cmd = new OracleCommand(plsql, connection);
#else
        await using var cmd = new OracleCommand(plsql, connection);
#endif

        var handleParam = new OracleParameter("handle", OracleDbType.Varchar2, 128)
        {
            Direction = ParameterDirection.Output
        };

        cmd.Parameters.Add(
            new OracleParameter("name", OracleDbType.Varchar2)
            {
                Value = lockKey,
                Direction = ParameterDirection.Input
            });
        cmd.Parameters.Add(handleParam);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return handleParam.Value?.ToString();
    }

    private static async Task<int> RequestLockAsync(
        OracleConnection connection,
        string lockHandle,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        const string plsql = """
                             BEGIN
                                 :result := DBMS_LOCK.REQUEST(:lockhandle, 6, :timeout, FALSE);
                             END;
                             """;

#if NETFRAMEWORK
        using var cmd = new OracleCommand(plsql, connection);
#else
        await using var cmd = new OracleCommand(plsql, connection);
#endif

        var resultParam = new OracleParameter("result", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add(resultParam);
        cmd.Parameters.Add("lockhandle", OracleDbType.Varchar2, lockHandle, ParameterDirection.Input);
        cmd.Parameters.Add("timeout", OracleDbType.Int32, timeoutSeconds, ParameterDirection.Input);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return Convert.ToInt32(resultParam.Value?.ToString());
    }

    private static async Task<int> ReleaseLockByHandleAsync(
        OracleConnection connection,
        string lockHandle,
        CancellationToken cancellationToken)
    {
        const string plsql = """
                             BEGIN
                                 :result := DBMS_LOCK.RELEASE(:lockhandle);
                             END;
                             """;

#if NETFRAMEWORK
        using var cmd = new OracleCommand(plsql, connection);
#else
        await using var cmd = new OracleCommand(plsql, connection);
#endif

        var resultParam = new OracleParameter("result", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add(resultParam);
        cmd.Parameters.Add("lockhandle", OracleDbType.Varchar2, lockHandle, ParameterDirection.Input);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return Convert.ToInt32(resultParam.Value?.ToString());
    }
}
