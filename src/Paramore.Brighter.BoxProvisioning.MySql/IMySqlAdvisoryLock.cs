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
/// Encapsulates the MySQL session-level advisory-lock primitive used to serialise box-table
/// migrations. Session-scoped: a lock acquired through <see cref="AcquireAsync"/> is held
/// until an explicit <see cref="ReleaseAsync"/> call or until the session terminates, and
/// crosses MySQL's per-DDL implicit commits unchanged.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation is <see cref="MySqlAdvisoryLock"/>. <see cref="MySqlBoxMigrationRunner"/>
/// accepts an injected instance through its constructor for testability and for advanced
/// integrators with custom connection-pool sharing or external lock-key derivation. Lock-key
/// derivation (the 64-char-safe transformation of the box table name) is performed by the
/// runner via <see cref="MySqlMigrationLockName.For"/>; the abstraction owns only the
/// <c>GET_LOCK</c> / <c>RELEASE_LOCK</c> SQL. See ADR 0057 §5b.
/// </para>
/// </remarks>
public interface IMySqlAdvisoryLock
{
    /// <summary>
    /// Acquires a session-level advisory lock named by <paramref name="lockKey"/>.
    /// </summary>
    /// <param name="connection">The open <see cref="MySqlConnection"/> on which to execute
    /// <c>GET_LOCK</c>. The lock is bound to this connection's session.</param>
    /// <param name="lockKey">The lock name to pass to <c>GET_LOCK</c>; must already be
    /// within MySQL's 64-character limit (callers should derive via
    /// <see cref="MySqlMigrationLockName.For"/>).</param>
    /// <param name="timeout">Maximum wait, mapped to <c>GET_LOCK</c>'s timeout argument
    /// (truncated to whole seconds).</param>
    /// <param name="cancellationToken">Cancellation token observed by the underlying
    /// command.</param>
    /// <returns>A task that completes when the lock has been acquired.</returns>
    /// <exception cref="TimeoutException">Thrown when the lock cannot be acquired within
    /// <paramref name="timeout"/>.</exception>
    Task AcquireAsync(
        MySqlConnection connection, string lockKey,
        TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Releases the session-level advisory lock named by <paramref name="lockKey"/>.
    /// </summary>
    /// <param name="connection">The same <see cref="MySqlConnection"/> (and session) on
    /// which the lock was acquired.</param>
    /// <param name="lockKey">The lock name used at acquisition.</param>
    /// <param name="cancellationToken">Cancellation token observed by the underlying
    /// command.</param>
    /// <returns>The three-valued result of MySQL's <c>RELEASE_LOCK</c>:
    /// <list type="bullet">
    ///   <item><description><c>true</c> — the lock existed and was released by this
    ///   session (normal case).</description></item>
    ///   <item><description><c>false</c> — the lock exists but was held by another session
    ///   (diagnostic anomaly: this session just acquired it).</description></item>
    ///   <item><description><c>null</c> — no lock by that name exists (diagnostic anomaly:
    ///   acquisition implied creation).</description></item>
    /// </list>
    /// Both non-true outcomes are surfaced through a Warning-level log entry on the runner;
    /// release does not throw.</returns>
    Task<bool?> ReleaseAsync(
        MySqlConnection connection, string lockKey,
        CancellationToken cancellationToken);
}
