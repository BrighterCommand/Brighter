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
/// Encapsulates the SQL Server transaction-scoped advisory-lock primitive used to serialise
/// box-table migrations. The lock is acquired with <c>@LockOwner = 'Transaction'</c>, so it
/// is automatically released when the surrounding <see cref="SqlTransaction"/> commits or
/// rolls back — the abstraction is therefore acquire-only and exposes no explicit release
/// operation, in contrast to the session-scoped Postgres / MySQL siblings.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation is <see cref="MsSqlAdvisoryLock"/>.
/// <see cref="MsSqlBoxMigrationRunner"/> accepts an injected instance through its
/// constructor for testability and for advanced integrators with custom lock-resource
/// derivation (Vault, KMS, etc.). See ADR 0057 §5b for the design.
/// </para>
/// <para>
/// Implementations must distinguish the four documented <c>sp_getapplock</c> failure modes
/// by exception type so an operator can react appropriately:
/// <list type="bullet">
///   <item><description><c>-1</c> (timeout) → <see cref="TimeoutException"/></description></item>
///   <item><description><c>-2</c> (cancelled) → <see cref="OperationCanceledException"/></description></item>
///   <item><description><c>-3</c> (deadlock victim) → <see cref="MigrationLockDeadlockException"/></description></item>
///   <item><description><c>-999</c> (parameter validation / call error) → <see cref="ArgumentException"/></description></item>
/// </list>
/// </para>
/// </remarks>
public interface IMsSqlAdvisoryLock
{
    /// <summary>
    /// Acquires a transaction-scoped exclusive advisory lock named by
    /// <paramref name="lockResource"/>. The lock is released automatically when
    /// <paramref name="transaction"/> commits or rolls back.
    /// </summary>
    /// <param name="connection">The open <see cref="SqlConnection"/> on which to execute
    /// <c>sp_getapplock</c>.</param>
    /// <param name="transaction">The active <see cref="SqlTransaction"/> that owns the lock.
    /// Must be enlisted on the same <paramref name="connection"/>.</param>
    /// <param name="lockResource">Resource name passed as <c>@Resource</c> to
    /// <c>sp_getapplock</c>. SQL Server limits this to 255 characters.</param>
    /// <param name="timeout">Maximum total wait before <c>sp_getapplock</c> returns <c>-1</c>.</param>
    /// <param name="cancellationToken">Cancellation token observed during the underlying SQL
    /// command.</param>
    /// <returns>A task that completes when the lock has been acquired.</returns>
    /// <exception cref="TimeoutException">Thrown when <c>sp_getapplock</c> returns <c>-1</c>
    /// — the lock could not be acquired within <paramref name="timeout"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <c>sp_getapplock</c> returns
    /// <c>-2</c> or when <paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="MigrationLockDeadlockException">Thrown when <c>sp_getapplock</c>
    /// returns <c>-3</c> — the calling session was chosen as a deadlock victim.</exception>
    /// <exception cref="ArgumentException">Thrown when <c>sp_getapplock</c> returns
    /// <c>-999</c> (parameter validation / call error) or when
    /// <paramref name="lockResource"/> exceeds the 255-character SQL Server limit.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeout"/>
    /// is negative or when its <c>TotalMilliseconds</c> exceeds <c>int.MaxValue</c>
    /// (~24.85 days) — <c>sp_getapplock</c>'s INT <c>@LockTimeout</c> argument would
    /// silently overflow on cast.</exception>
    Task AcquireAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string lockResource,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
