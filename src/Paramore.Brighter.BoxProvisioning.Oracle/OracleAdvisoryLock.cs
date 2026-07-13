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
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout,
                "Migration lock timeout must be non-negative.");

        var timeoutSeconds = (int)timeout.TotalSeconds;
        var lockHandle = await AllocateHandleAsync(connection, lockKey, cancellationToken);

#if NETFRAMEWORK
        using var cmd = connection.CreateCommand();
#else
        await using var cmd = connection.CreateCommand();
#endif
        cmd.BindByName = true;
        cmd.CommandText = """
                          BEGIN 
                            :Result := DBMS_LOCK.REQUEST(:LockHandle, DBMS_LOCK.X_MODE, :Timeout, FALSE); 
                          END;
                          """;

        var resultParam = new OracleParameter("Result", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add(resultParam);
        cmd.Parameters.Add(new OracleParameter("LockHandle", OracleDbType.Varchar2, ParameterDirection.Input) { Value = lockHandle });
        cmd.Parameters.Add(new OracleParameter("Timeout", OracleDbType.Int32) { Value = timeoutSeconds, Direction = ParameterDirection.Input });

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        var result = Convert.ToInt32(resultParam.Value!.ToString());

        switch (result)
        {
            case 0: return;
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
        string lockHandle;
        try
        {
            lockHandle = await AllocateHandleAsync(connection, lockKey, cancellationToken);
        }
        catch
        {
            return null;
        }

#if NETFRAMEWORK
        using var cmd = connection.CreateCommand();
#else
        await using var cmd = connection.CreateCommand();
#endif

        cmd.BindByName = true;
        cmd.CommandText = "BEGIN :Result := DBMS_LOCK.RELEASE(:LockHandle); END;";

        var resultParam = new OracleParameter("Result", OracleDbType.Int32)
            { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(resultParam);
        cmd.Parameters.Add(new OracleParameter("LockHandle", OracleDbType.Varchar2)
            { Value = lockHandle, Direction = ParameterDirection.Input });

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return Convert.ToInt32(resultParam.Value.ToString()) == 0;
    }

    private static async Task<string> AllocateHandleAsync(
        OracleConnection connection, string lockKey,
        CancellationToken cancellationToken)
    {
#if NETFRAMEWORK
        using var cmd = connection.CreateCommand();
#else
        await using var cmd = connection.CreateCommand();
#endif

        cmd.BindByName = true;
        cmd.CommandText =
            "BEGIN DBMS_LOCK.ALLOCATE_UNIQUE(:LockName, :LockHandle); END;";

        cmd.Parameters.Add(new OracleParameter("LockName", OracleDbType.Varchar2)
            { Value = lockKey, Direction = ParameterDirection.Input });
        var handleParam = new OracleParameter("LockHandle", OracleDbType.Varchar2, 128)
            { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(handleParam);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return handleParam.Value.ToString()!;
    }
}
