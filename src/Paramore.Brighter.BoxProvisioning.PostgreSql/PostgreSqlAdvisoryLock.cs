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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Default <see cref="IPostgreSqlAdvisoryLock"/> backed by Postgres
/// <c>pg_try_advisory_lock(bigint)</c> / <c>pg_advisory_unlock(bigint)</c>. Uses an
/// exponential backoff retry loop bounded by the supplied timeout for acquisition.
/// </summary>
/// <remarks>
/// The deadline is measured against an injectable <see cref="TimeProvider"/>; the default
/// <see cref="TimeProvider.System"/> reads <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>
/// (monotonic), so a wall-clock jump (NTP correction during a long lock wait, leap-second
/// smear, container clock skew on VM resume) cannot collapse or extend the budget.
/// <para>
/// Lock-key derivation: the per-call key (typically <c>BrighterMigration_&lt;schema&gt;.&lt;table&gt;</c>
/// from the runner) is prefixed with a Brighter namespace as <c>"74726:&lt;lockKey&gt;"</c>, hashed
/// with SHA-256, and the first 8 bytes of the digest are read big-endian into the signed
/// <c>bigint</c> passed to <c>pg_try_advisory_lock</c> / <c>pg_advisory_unlock</c>. Reading
/// big-endian keeps the key identical on every host. A collision between two lock keys
/// (about 1 in 2^64) only serialises two migrations on one lock. See ADR 0062.
/// </para>
/// </remarks>
public class PostgreSqlAdvisoryLock : IPostgreSqlAdvisoryLock
{
    // Prefixed to every lock key before hashing, to keep Brighter's advisory locks apart from
    // other applications' (ADR 0062).
    private const int BRIGHTER_LOCK_NAMESPACE = 74726;

    private readonly TimeProvider _timeProvider;

    private static long DeriveLockKey(string lockKey)
    {
        var composite = $"{BRIGHTER_LOCK_NAMESPACE}:{lockKey}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(composite));
        return System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(hash);
    }

    /// <summary>
    /// Initialises a new <see cref="PostgreSqlAdvisoryLock"/>.
    /// </summary>
    /// <param name="timeProvider">Source of timestamps for the acquisition deadline. Defaults
    /// to <see cref="TimeProvider.System"/> (monotonic).</param>
    public PostgreSqlAdvisoryLock(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task AcquireAsync(
        NpgsqlConnection connection, string lockKey,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        var startTimestamp = _timeProvider.GetTimestamp();
        var delayMs = 100;

        while (true)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(@key)";
            command.Parameters.AddWithValue("@key", DeriveLockKey(lockKey));

            var raw = await command.ExecuteScalarAsync(cancellationToken);
            var result = raw is bool b
                ? b
                : throw new InvalidOperationException(
                    $"pg_try_advisory_lock for '{lockKey}' returned null (expected boolean).");
            if (result) return;

            if (_timeProvider.GetElapsedTime(startTimestamp) >= timeout)
            {
                throw new TimeoutException(
                    $"Could not acquire migration lock for '{lockKey}' within {timeout.TotalSeconds}s.");
            }

            await Task.Delay(delayMs, cancellationToken);
            delayMs = Math.Min(delayMs * 2, 1000);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ReleaseAsync(
        NpgsqlConnection connection, string lockKey,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(@key)";
        command.Parameters.AddWithValue("@key", DeriveLockKey(lockKey));

        var raw = await command.ExecuteScalarAsync(cancellationToken);
        return raw is bool released && released;
    }
}
