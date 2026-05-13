#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Default <see cref="IMsSqlAdvisoryLock"/> backed by SQL Server's <c>sp_getapplock</c>
/// system stored procedure. Acquires an exclusive transaction-scoped lock; the lock is
/// released automatically when the surrounding transaction commits or rolls back.
/// </summary>
/// <remarks>
/// <para>
/// The implementation translates each documented <c>sp_getapplock</c> negative return code
/// into a distinct exception type so an operator can react with the right strategy — see
/// <see cref="IMsSqlAdvisoryLock"/> for the full mapping. Non-negative returns (<c>0</c> or
/// <c>1</c>) are both successes; the latter indicates the lock was granted after a brief
/// wait.
/// </para>
/// </remarks>
public class MsSqlAdvisoryLock : IMsSqlAdvisoryLock
{
    /// <inheritdoc />
    public async Task AcquireAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string lockResource,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ValidateLockParameters(lockResource, timeout);

        var result = await TakeLock(connection, transaction, lockResource, timeout, cancellationToken);

        EvaluateLockResponse(result, lockResource, timeout, cancellationToken);
    }

    private static async Task<int> TakeLock(
        SqlConnection connection, 
        SqlTransaction transaction, 
        string lockResource,
        TimeSpan timeout, 
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "DECLARE @result INT; " +
            "EXEC @result = sp_getapplock " +
            "@Resource = @lockResourceName, " +
            "@LockMode = 'Exclusive', " +
            "@LockTimeout = @lockTimeoutMs, " +
            "@LockOwner = 'Transaction'; " +
            "SELECT @result;";
        command.Parameters.AddWithValue("@lockResourceName", lockResource);
        command.Parameters.AddWithValue("@lockTimeoutMs", (int)timeout.TotalMilliseconds);

        // sp_getapplock returns an INT — never NULL per the documented contract. Pattern-match
        // so a future provider/driver bug surfaces as a named InvalidOperationException rather
        // than a bare NullReferenceException (e.g. MS.Data.SqlClient has been observed to
        // return null from ExecuteScalarAsync under server-side errors).
        var raw = await command.ExecuteScalarAsync(cancellationToken);
        return raw is int i
            ? i
            : throw new InvalidOperationException(
                $"sp_getapplock on '{lockResource}' returned null (expected INT result code).");
    }

    private static void EvaluateLockResponse(int result, string lockResource, TimeSpan timeout, CancellationToken cancellationToken)
    {
        // sp_getapplock contract:
        //   0  → lock granted synchronously
        //   1  → lock granted after a brief wait (treat as success)
        //   -1 → wait timeout expired
        //   -2 → request cancelled
        //   -3 → calling session was chosen as a deadlock victim
        //   -999 → parameter validation / call error
        switch (result)
        {
            case >= 0:
                return;
            case -1:
                throw new TimeoutException(
                    $"Could not acquire migration lock for '{lockResource}' within {timeout.TotalSeconds}s. " +
                    $"sp_getapplock returned -1.");
            case -2:
                throw new OperationCanceledException(
                    $"Acquisition of migration lock for '{lockResource}' was cancelled. " +
                    $"sp_getapplock returned -2.",
                    cancellationToken);
            case -3:
                throw new MigrationLockDeadlockException(
                    $"Migration lock for '{lockResource}' was chosen as a deadlock victim. " +
                    $"sp_getapplock returned -3.");
            case -999:
                throw new ArgumentException(
                    $"sp_getapplock parameter validation failed for '{lockResource}'. " +
                    $"sp_getapplock returned -999.",
                    nameof(lockResource));
            default:
                throw new InvalidOperationException(
                    $"sp_getapplock returned an unexpected code {result} for '{lockResource}'.");
        }
    }

    private static void ValidateLockParameters(string lockResource, TimeSpan timeout)
    {
        // sp_getapplock takes @LockTimeout as a SQL Server INT (milliseconds). A negative
        // TimeSpan has no meaningful interpretation for an exclusive application lock, and a
        // value whose TotalMilliseconds exceeds int.MaxValue (~24.85 days) silently overflows
        // on cast and may produce -1 — which sp_getapplock interprets as "wait indefinitely".
        // Validate up front so the failure mode is an actionable ArgumentOutOfRangeException
        // rather than a deadlocked deployment.
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Migration lock timeout must be non-negative.");
        }

        if (timeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout), timeout,
                $"Migration lock timeout must not exceed {TimeSpan.FromMilliseconds(int.MaxValue)} " +
                $"(int.MaxValue ms ≈ 24.85 days). sp_getapplock would silently overflow on cast.");
        }

        // SQL Server limits @Resource to 255 characters. Reject longer resources up front
        // so the failure mode is an actionable ArgumentException rather than an opaque
        // sp_getapplock -999 (parameter validation) at runtime.
        if (lockResource.Length > 255)
        {
            throw new ArgumentException(
                $"sp_getapplock @Resource '{lockResource}' exceeds the 255-character limit " +
                $"(supplied {lockResource.Length} chars). Use a shorter lock resource.",
                nameof(lockResource));
        }
    }
}
