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
using MySqlConnector;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Default <see cref="IMySqlAdvisoryLock"/> backed by MySQL <c>GET_LOCK(name, timeout)</c>
/// and <c>RELEASE_LOCK(name)</c>. Acquire blocks server-side for up to the supplied timeout
/// and throws <see cref="TimeoutException"/> on failure.
/// </summary>
/// <remarks>
/// Operator note — fail-fast is unavailable on MySQL. <c>GET_LOCK</c> takes whole seconds
/// (SQL <c>INT</c> bind), and a sub-second wait of <c>TimeSpan.Zero</c> would round to a
/// non-blocking acquire that races the migration. <see cref="AcquireAsync"/> floors the wait
/// at 1 second instead, so a configured <c>TimeSpan.Zero</c> still produces a server-side
/// blocking acquire of one second — not the "try once and fail immediately" semantics
/// available on MSSQL (<c>sp_getapplock</c> with <c>LockTimeout = 0</c>) or PostgreSQL
/// (<c>pg_try_advisory_lock</c>). Callers needing strict fail-fast should detect this
/// constraint at configuration time on the MySQL backend.
/// </remarks>
public class MySqlAdvisoryLock : IMySqlAdvisoryLock
{
    /// <inheritdoc />
    public async Task AcquireAsync(
        MySqlConnection connection, string lockKey,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        ValidateLockParameters(timeout);

        var timeoutSeconds = ToWholeSecondsAtLeastOne(timeout);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT GET_LOCK(@LockName, @Timeout)";
        command.Parameters.AddWithValue("@LockName", lockKey);
        command.Parameters.AddWithValue("@Timeout", timeoutSeconds);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        if (result is null or DBNull)
        {
            throw new MySqlAdvisoryLockException(
                $"GET_LOCK on '{lockKey}' returned NULL — likely a server-side error " +
                "(out of memory, KILLed connection, or memory-table fault). Check server logs.");
        }

        if (Convert.ToInt32(result) != 1)
        {
            throw new TimeoutException(
                $"Could not acquire migration lock '{lockKey}' within {timeoutSeconds}s.");
        }
    }

    /// <inheritdoc />
    public async Task<bool?> ReleaseAsync(
        MySqlConnection connection, string lockKey,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT RELEASE_LOCK(@LockName)";
        command.Parameters.AddWithValue("@LockName", lockKey);

        var raw = await command.ExecuteScalarAsync(cancellationToken);
        if (raw == null || raw is DBNull) return null;
        return Convert.ToInt32(raw) == 1;
    }

    // GET_LOCK takes whole seconds, and GET_LOCK(name, 0) does not block; a sub-second timeout
    // rounded down to 0 would let the migration run unlocked.
    private static int ToWholeSecondsAtLeastOne(TimeSpan timeout)
        => (int)Math.Max(1, Math.Ceiling(timeout.TotalSeconds));

    private static void ValidateLockParameters(TimeSpan timeout)
    {
        ThrowIfTimeoutNegative(timeout);
        ThrowIfTimeoutOutOfRange(timeout);
    }

    // The one-second floor would otherwise silently turn a negative timeout into a valid wait.
    private static void ThrowIfTimeoutNegative(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Migration lock timeout must be non-negative.");
        }
    }

    // GET_LOCK takes an INT of seconds. A timeout past int.MaxValue seconds overflows on the cast
    // to a negative number, which GET_LOCK reads as "wait forever".
    private static void ThrowIfTimeoutOutOfRange(TimeSpan timeout)
    {
        if (timeout.TotalSeconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout), timeout,
                $"Migration lock timeout must not exceed {TimeSpan.FromSeconds(int.MaxValue)} " +
                $"(int.MaxValue seconds ≈ 68 years). GET_LOCK would silently overflow on cast.");
        }
    }
}
