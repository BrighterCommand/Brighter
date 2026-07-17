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
/// Encapsulates the Postgres session-level advisory-lock primitive used to serialise
/// box-table migrations. Session-scoped: a lock acquired through <see cref="AcquireAsync"/>
/// outlives any transaction on the same connection and is released either by an explicit
/// <see cref="ReleaseAsync"/> call or by the connection closing.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation is <see cref="PostgreSqlAdvisoryLock"/>. <see cref="PostgreSqlBoxMigrationRunner"/>
/// accepts an injected instance through its constructor for testability and for advanced
/// integrators with custom connection-pool sharing or external lock-key derivation (Vault,
/// KMS, etc.). See ADR 0057 §5b for the design.
/// </para>
/// </remarks>
public interface IPostgreSqlAdvisoryLock
{
    /// <summary>
    /// Acquires a session-level advisory lock named by <paramref name="lockKey"/>, retrying
    /// until the lock is held or <paramref name="timeout"/> elapses.
    /// </summary>
    /// <param name="connection">The open <see cref="NpgsqlConnection"/> on which to execute
    /// the lock primitive. The lock is bound to this connection's session.</param>
    /// <param name="lockKey">Symbolic key for the lock — the implementation hashes this into
    /// the integer space that Postgres advisory locks operate on.</param>
    /// <param name="timeout">Maximum total wait before giving up. The implementation may use
    /// any retry/backoff strategy within this budget.</param>
    /// <param name="cancellationToken">Cancellation token observed during retry sleeps.</param>
    /// <returns>A task that completes when the lock has been acquired.</returns>
    /// <exception cref="TimeoutException">Thrown when the lock cannot be acquired within
    /// <paramref name="timeout"/>.</exception>
    Task AcquireAsync(
        NpgsqlConnection connection, string lockKey,
        TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Releases the session-level advisory lock named by <paramref name="lockKey"/>.
    /// </summary>
    /// <param name="connection">The same <see cref="NpgsqlConnection"/> (and session) on
    /// which the lock was acquired.</param>
    /// <param name="lockKey">The symbolic key used at acquisition.</param>
    /// <param name="cancellationToken">Cancellation token observed by the underlying SQL
    /// command.</param>
    /// <returns><c>true</c> if the calling session held the lock at release time;
    /// <c>false</c> otherwise. A <c>false</c> result is a diagnostic anomaly (the caller
    /// asked to release a lock it does not currently hold) and is surfaced through a
    /// Warning-level log entry on the runner — release does not throw.</returns>
    Task<bool> ReleaseAsync(
        NpgsqlConnection connection, string lockKey,
        CancellationToken cancellationToken);
}
