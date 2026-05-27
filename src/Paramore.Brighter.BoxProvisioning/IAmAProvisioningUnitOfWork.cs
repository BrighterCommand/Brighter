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
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// The unit-of-work role for a single migration run. Encapsulates the per-backend
/// pairing of advisory lock and transaction (where present) so that the
/// template-method runner can drive the lifecycle without knowing the backend's
/// specific lock+tx ordering.
/// </summary>
/// <remarks>
/// One implementation per relational backend (MSSQL, Postgres, MySQL, SQLite).
/// Spanner is exempt per ADR 0057 §6 — its degenerate runner does not derive
/// from the relational base and does not consume a UoW.
/// <para>
/// The template-method runner declares the UoW with <c>await using</c>, so
/// <see cref="IAsyncDisposable.DisposeAsync"/> is invoked by the language on
/// every exit path — including when <see cref="BeginAsync"/> itself throws.
/// Implementations MUST therefore tolerate Dispose after a failed/skipped
/// BeginAsync as a no-op or partial cleanup (see <see cref="BeginAsync"/>
/// XML-doc).
/// </para>
/// </remarks>
/// <typeparam name="TTransaction">The backend-specific <see cref="DbTransaction"/> subtype.
/// Transactionless backends (MySQL — see ADR 0057 §5a) declare a transaction subtype for
/// symmetry but never return a non-null value from <see cref="Transaction"/>.</typeparam>
public interface IAmAProvisioningUnitOfWork<TTransaction> : IAsyncDisposable
    where TTransaction : DbTransaction
{
    /// <summary>
    /// The active transaction, if this backend uses one. Null for transactionless
    /// backends (MySQL — see ADR 0057 §5a). Detection helpers and path methods
    /// receive this value via the runner so they can participate in the atomic
    /// scope established by <see cref="BeginAsync"/>.
    /// </summary>
    TTransaction? Transaction { get; }

    /// <summary>
    /// Opens the atomic scope: acquires the advisory lock and begins the transaction
    /// (or both, in whichever order this backend requires). Throws on lock-acquisition
    /// timeout or transaction-begin failure.
    /// </summary>
    /// <remarks>
    /// The runner declares the UoW with <c>await using</c>, so
    /// <see cref="IAsyncDisposable.DisposeAsync"/> is invoked on every exit path —
    /// including when this method itself throws. Implementations MUST therefore be
    /// safe for <c>DisposeAsync</c> after a failed/skipped <c>BeginAsync</c>: the
    /// dispose path runs as a no-op or partial cleanup and never throws. After a
    /// thrown <c>BeginAsync</c>, the runner does NOT call <see cref="CommitAsync"/>
    /// or <see cref="RollbackAsync"/> — see ADR 0058 §B.3 for the harmonised
    /// lifecycle contract.
    /// </remarks>
    /// <param name="lockResource">The backend-specific lock resource identifier
    /// (e.g. an MSSQL <c>sp_getapplock</c> name, a Postgres <c>pg_advisory_lock</c>
    /// 64-bit key, a MySQL <c>GET_LOCK</c> name).</param>
    /// <param name="lockTimeout">How long to wait for the advisory lock before
    /// throwing.</param>
    /// <param name="cancellationToken">Cancellation. Cancellation during
    /// <c>BeginAsync</c> is treated identically to any other exception thrown from
    /// this method — the runner does NOT call <c>CommitAsync</c> or
    /// <c>RollbackAsync</c>; <c>await using</c> still calls <c>DisposeAsync</c>.</param>
    Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken);

    /// <summary>
    /// Commits the atomic scope. Releases the lock (where lock release is explicit
    /// rather than transaction-scoped). After <c>CommitAsync</c>, the only valid
    /// call is <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// </summary>
    /// <remarks>
    /// If <c>CommitAsync</c> throws, the runner enters the catch path and calls
    /// <see cref="RollbackAsync"/> with <see cref="CancellationToken.None"/> —
    /// even though commit was attempted. Implementations MUST make
    /// <c>RollbackAsync</c> best-effort after a thrown <c>CommitAsync</c>:
    /// inspect the underlying transaction state (e.g. MSSQL <c>Zombied</c>,
    /// Postgres <c>TransactionStatus.Closed</c>) and skip rollback if the
    /// transaction is already finalised. See ADR 0058 §B.3.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation.</param>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Rolls back the atomic scope. Releases the lock (where lock release is
    /// explicit rather than transaction-scoped). After <c>RollbackAsync</c>, the
    /// only valid call is <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// </summary>
    /// <remarks>
    /// <c>RollbackAsync</c> MUST NOT throw — disposal-style semantics. If the
    /// underlying transaction-rollback or lock-release fails internally, the
    /// implementation logs a Warning (with backend-specific diagnostic — e.g.
    /// MySQL preserves its <c>RELEASE_LOCK</c> tri-state per spec 0027 Item M /
    /// ADR 0057 §5b) and returns. The runner passes
    /// <see cref="CancellationToken.None"/> on rollback (NOT the caller's token)
    /// so a signalled token does not abandon the unwind. See ADR 0058 §B.3.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation. The runner passes
    /// <see cref="CancellationToken.None"/> from the catch path; direct callers
    /// may pass a cancellable token but should not expect cancellation to abort
    /// the rollback.</param>
    Task RollbackAsync(CancellationToken cancellationToken);
}
