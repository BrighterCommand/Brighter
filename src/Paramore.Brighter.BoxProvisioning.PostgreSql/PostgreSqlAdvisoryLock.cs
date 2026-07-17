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
/// smear, container clock skew on VM resume) cannot collapse or extend the budget. Tests
/// inject a fake provider to drive the deadline check deterministically.
/// <para>
/// Lock-key derivation: the per-call key (typically <c>BrighterMigration_&lt;schema&gt;.&lt;table&gt;</c>
/// from the runner) is combined with the Brighter namespace constant into the composite
/// <c>"74726:&lt;lockKey&gt;"</c>, hashed with SHA-256, and the first 8 bytes of the digest are read
/// fixed big-endian (via <c>BinaryPrimitives.ReadInt64BigEndian</c>) into a signed <c>long</c>.
/// That single 64-bit value is passed to the single-argument <c>pg_try_advisory_lock(bigint)</c> /
/// <c>pg_advisory_unlock(bigint)</c> overloads; there is no <c>hashtext</c> call and the namespace
/// is folded into the hash input rather than passed as a separate SQL argument. The birthday-bound
/// collision probability across distinct lock keys is therefore ~1 in 2^64, hardened from the
/// ~1 in 2^32 of the prior <c>hashtext → int4</c> scheme and aligned with the 64-bit SHA-256
/// principle the MySQL long-form fallback in <c>MySqlMigrationLockName</c> follows. Any collision
/// merely serialises two migrations on a shared advisory lock (correctness preserved, only the
/// concurrency boundary widens), and the per-deployment population is typically &lt; 100 box tables.
/// Fixing big-endian keeps the derived key identical on every host, OS, and culture (deterministic);
/// see ADR 0062.
/// </para>
/// </remarks>
public class PostgreSqlAdvisoryLock : IPostgreSqlAdvisoryLock
{
    // Brighter-specific advisory-lock namespace, folded into the hash input by DeriveLockKey (the
    // composite prefix "74726:<lockKey>") rather than passed as a separate SQL argument. The single
    // 64-bit advisory-lock space is partitioned per box table by the lock-key string; the namespace
    // distinguishes Brighter's locks from any other application using the same composite scheme. The
    // numeric value is "BRIG" interpreted as ASCII (74726 decimal); not load-bearing — collision
    // with another application's choice is diagnostic not correctness.
    private const int BRIGHTER_LOCK_NAMESPACE = 74726;

    private readonly TimeProvider _timeProvider;

    // Derives the 64-bit advisory-lock key for a Brighter lock key. The namespace is folded into a
    // composite ("74726:<lockKey>"), hashed with SHA-256, and the first 8 bytes are read fixed
    // big-endian into a signed long (ADR 0062). Both AcquireAsync and ReleaseAsync route through
    // this single helper, so they always derive byte-identical keys for the same lockKey.
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
