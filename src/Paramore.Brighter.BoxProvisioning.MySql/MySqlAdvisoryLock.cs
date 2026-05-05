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
        var timeoutSeconds = (int)timeout.TotalSeconds;

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT GET_LOCK(@LockName, @Timeout)";
        command.Parameters.AddWithValue("@LockName", lockKey);
        command.Parameters.AddWithValue("@Timeout", timeoutSeconds);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result == null || Convert.ToInt32(result) != 1)
        {
            throw new TimeoutException(
                $"Could not acquire migration lock '{lockKey}' within {timeout.TotalSeconds}s.");
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
