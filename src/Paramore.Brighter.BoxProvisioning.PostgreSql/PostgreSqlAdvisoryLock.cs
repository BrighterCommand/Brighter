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
using Npgsql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Default <see cref="IPostgreSqlAdvisoryLock"/> backed by Postgres
/// <c>pg_try_advisory_lock(int4, int4)</c> / <c>pg_advisory_unlock(int4, int4)</c>. Uses an
/// exponential backoff retry loop bounded by the supplied timeout for acquisition.
/// </summary>
/// <remarks>
/// The deadline is measured against an injectable <see cref="TimeProvider"/>; the default
/// <see cref="TimeProvider.System"/> reads <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>
/// (monotonic), so a wall-clock jump (NTP correction during a long lock wait, leap-second
/// smear, container clock skew on VM resume) cannot collapse or extend the budget. Tests
/// inject a fake provider to drive the deadline check deterministically.
/// <para>
/// Lock-key hashing: the per-call key (typically <c>BrighterMigration_&lt;schema&gt;.&lt;table&gt;</c>
/// from the runner) is fed through Postgres <c>hashtext(text) → int4</c> to fit the 32-bit
/// second argument of the <c>(int4, int4)</c> overload of <c>pg_try_advisory_lock</c>. The
/// birthday-bound collision probability across distinct lock keys is therefore ~1 in 2^32
/// (~4 billion), mirroring the MySQL lock-name truncation note in
/// <c>MySqlMigrationLockName</c> — accepted as negligible given the per-deployment
/// population is typically &lt; 100 box tables, and any collision merely serialises two
/// migrations on a shared advisory lock (correctness preserved, only the concurrency boundary
/// widens). The <c>(bigint)</c> overload of <c>pg_try_advisory_lock</c> with a SHA-256-derived
/// 64-bit key would push this to ~1 in 2^64 — tracked as a follow-up at
/// <see href="https://github.com/BrighterCommand/Brighter/issues/4145"/>.
/// </para>
/// </remarks>
public class PostgreSqlAdvisoryLock : IPostgreSqlAdvisoryLock
{
    // Brighter-specific advisory-lock namespace. Postgres advisory locks operate in a single
    // 64-bit integer space (or two int4s); BRIGHTER_LOCK_NAMESPACE is the int4 namespace
    // distinguishing Brighter's locks from any other application that uses advisory locks
    // against the same database. The numeric value is "BRIG" interpreted as ASCII (74726
    // decimal); not load-bearing — collision with another application's choice is diagnostic
    // not correctness, since the lock-key string further partitions per box table.
    private const int BRIGHTER_LOCK_NAMESPACE = 74726;

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initialises a new <see cref="PostgreSqlAdvisoryLock"/>.
    /// </summary>
    /// <param name="timeProvider">Source of timestamps for the acquisition deadline. Defaults
    /// to <see cref="TimeProvider.System"/> (monotonic). Tests pass
    /// <c>FakeTimeProvider</c> from <c>Microsoft.Extensions.TimeProvider.Testing</c>.</param>
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
            command.CommandText = "SELECT pg_try_advisory_lock(@ns, hashtext(@key))";
            command.Parameters.AddWithValue("@ns", BRIGHTER_LOCK_NAMESPACE);
            command.Parameters.AddWithValue("@key", lockKey);

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
        command.CommandText = "SELECT pg_advisory_unlock(@ns, hashtext(@key))";
        command.Parameters.AddWithValue("@ns", BRIGHTER_LOCK_NAMESPACE);
        command.Parameters.AddWithValue("@key", lockKey);

        var raw = await command.ExecuteScalarAsync(cancellationToken);
        return raw is bool released && released;
    }
}
