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
public class MySqlAdvisoryLock : IMySqlAdvisoryLock
{
    /// <inheritdoc />
    public async Task AcquireAsync(
        MySqlConnection connection, string lockKey,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        // GET_LOCK takes whole seconds; truncating sub-second TimeSpans to 0 makes the call
        // non-blocking, defeating the migration lock for callers that configure short timeouts.
        // Floor at 1 second so a 500ms timeout still produces server-side blocking.
        var timeoutSeconds = (int)Math.Max(1, Math.Ceiling(timeout.TotalSeconds));

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT GET_LOCK(@LockName, @Timeout)";
        command.Parameters.AddWithValue("@LockName", lockKey);
        command.Parameters.AddWithValue("@Timeout", timeoutSeconds);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        // GET_LOCK returns 1 (acquired), 0 (timeout), or NULL (server-side error: OOM, KILLed
        // session, memory-table fault). Distinguish the latter from a timeout so operators
        // reading logs do not chase a phantom contention issue. NULL is unreachable on
        // current MySQL 8 paths in practice (NULL/invalid lock names raise a typed
        // MySqlException at parse time before GET_LOCK runs); kept as defensive coverage and
        // for parity with MSSQL's per-return-code mapping (Item N).
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
}
