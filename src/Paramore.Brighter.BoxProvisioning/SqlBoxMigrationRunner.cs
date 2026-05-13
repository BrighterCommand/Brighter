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
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Template-method base class implementing <see cref="IAmABoxMigrationRunner"/> for the
/// four relational backends (MSSQL, Postgres, MySQL, SQLite). The base owns the
/// success/failure orchestration; derived classes supply only the irreducibly-backend-specific
/// hooks. Spanner does NOT derive from this base — its degenerate fresh-install-only model
/// (per ADR 0057 §6) bypasses the bootstrap branch entirely and implements
/// <see cref="IAmABoxMigrationRunner"/> directly.
/// </summary>
/// <remarks>
/// The success-path order is documented in ADR 0058 §B.3: open connection → create UoW →
/// <c>BeginAsync</c> → ensure history table → re-detect state under the UoW (TOCTOU defence
/// per ADR 0057 §3) → dispatch to one of <c>RunFreshPath</c> / <c>RunBootstrapPath</c> /
/// <c>RunNormalPath</c> → <c>CommitAsync</c>. On any exception thrown between
/// <c>BeginAsync</c> and <c>CommitAsync</c> the runner calls
/// <c>uow.RollbackAsync(CancellationToken.None)</c> and rethrows.
/// </remarks>
/// <typeparam name="TConnection">The backend-specific <see cref="DbConnection"/> subtype.</typeparam>
/// <typeparam name="TTransaction">The backend-specific <see cref="DbTransaction"/> subtype.</typeparam>
public abstract class SqlBoxMigrationRunner<TConnection, TTransaction>
    : IAmABoxMigrationRunner
    where TConnection : DbConnection
    where TTransaction : DbTransaction
{
    private readonly IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> _detectionHelper;
    private readonly IAmARelationalDatabaseConfiguration _configuration;
    private readonly TimeSpan _lockTimeout;

    /// <summary>Logger for the runner base AND for derived classes to forward into per-backend UoW construction.</summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// The detection helper, exposed to derived classes so the bootstrap-path hook can call
    /// <see cref="IAmAVersionDetectingMigrationHelper{TConnection,TTransaction}.DetectCurrentVersionAsync"/>.
    /// The base itself uses this helper for the default <see cref="RedetectStateAsync"/> implementation.
    /// </summary>
    protected IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> DetectionHelper => _detectionHelper;

    /// <summary>
    /// The relational database configuration, exposed to derived classes for use inside their
    /// <c>OpenConnectionAsync</c> hook (typically to read <c>ConnectionString</c>) and any other
    /// hook that needs configuration metadata.
    /// </summary>
    protected IAmARelationalDatabaseConfiguration Configuration => _configuration;

    /// <summary>
    /// Initialises the base runner.
    /// </summary>
    /// <param name="detectionHelper">The version-detecting helper used for the default
    /// <see cref="RedetectStateAsync"/> implementation and exposed to derived classes for
    /// version inference during bootstrap.</param>
    /// <param name="configuration">The relational database configuration, exposed to derived
    /// classes via the <see cref="Configuration"/> property.</param>
    /// <param name="lockTimeout">How long the per-backend UoW waits for the advisory lock
    /// before throwing.</param>
    /// <param name="logger">Optional logger. Defaults to <see cref="NullLogger.Instance"/>.</param>
    protected SqlBoxMigrationRunner(
        IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> detectionHelper,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        ILogger? logger = null)
    {
        _detectionHelper = detectionHelper;
        _configuration = configuration;
        _lockTimeout = lockTimeout;
        Logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task MigrateAsync(
        string tableName,
        string? schemaName,
        BoxType boxType,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        ValidateMigrationsMonotonic(schemaName, tableName, migrations);

        // Sync `using` for the connection: DbConnection does not implement IAsyncDisposable
        // on netstandard2.0, so `await using` would not compile across the shared-assembly
        // TFM matrix. The UoW does implement IAsyncDisposable (see IAmAProvisioningUnitOfWork)
        // and is declared with `await using` below.
        using var connection = await OpenConnectionAsync(cancellationToken);

        var lockResource = LockResourceFor(schemaName, tableName);
        await using var uow = await CreateUnitOfWorkAsync(connection, cancellationToken);
        await uow.BeginAsync(lockResource, _lockTimeout, cancellationToken);

        try
        {
            await EnsureHistoryTableAsync(connection, uow.Transaction, schemaName, cancellationToken);

            var (tableExists, historyExists) = await RedetectStateAsync(
                connection, uow.Transaction, schemaName, tableName, cancellationToken);

            if (!tableExists)
            {
                await RunFreshPathAsync(
                    connection, uow.Transaction, schemaName, tableName, migrations, cancellationToken);
            }
            else if (!historyExists)
            {
                await RunBootstrapPathAsync(
                    connection, uow.Transaction, schemaName, tableName, boxType, migrations, cancellationToken);
            }
            else
            {
                await RunNormalPathAsync(
                    connection, uow.Transaction, schemaName, tableName, migrations, cancellationToken);
            }

            await uow.CommitAsync(cancellationToken);
        }
        catch
        {
            // Pass CancellationToken.None: a signalled caller token must not abandon the unwind.
            // See ADR 0058 §B.3 cancellation contract.
            await uow.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Opens a fresh <typeparamref name="TConnection"/> ready for use by the migration. Typical
    /// implementation reads <see cref="Configuration"/>.<c>ConnectionString</c> and calls
    /// <see cref="DbConnection.OpenAsync(CancellationToken)"/>.
    /// </summary>
    protected abstract Task<TConnection> OpenConnectionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Constructs the per-backend <see cref="IAmAProvisioningUnitOfWork{TTransaction}"/> over
    /// <paramref name="connection"/>. The runner declares the result with <c>await using</c>
    /// and immediately calls <c>BeginAsync</c>; the implementation should NOT begin the
    /// lock+transaction here, only construct the UoW.
    /// </summary>
    protected abstract Task<IAmAProvisioningUnitOfWork<TTransaction>> CreateUnitOfWorkAsync(
        TConnection connection, CancellationToken cancellationToken);

    /// <summary>
    /// Computes the backend-specific advisory lock resource string for the given table.
    /// Each backend's lock-resource format differs (MSSQL/PG: <c>BrighterMigration_{schema}.{table}</c>;
    /// MySQL: hashed with 64-char fallback; SQLite: ignored).
    /// </summary>
    protected abstract string LockResourceFor(string? schemaName, string tableName);

    /// <summary>
    /// Ensures the migration-history table exists. Each backend's history-table DDL differs;
    /// the base orchestrates the call but does not own the DDL.
    /// </summary>
    protected abstract Task EnsureHistoryTableAsync(
        TConnection connection, TTransaction? transaction, string? schemaName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies the fresh-install path: stamps the table at V_latest using V1's UpScript.
    /// Invoked when re-detection inside the UoW reports no table.
    /// </summary>
    protected abstract Task RunFreshPathAsync(
        TConnection connection, TTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the bootstrap (legacy-table) path: infers the in-place version via
    /// <c>DetectionHelper.DetectCurrentVersionAsync</c>, writes a synthesised history row, and
    /// catches up with any newer migrations. Invoked when re-detection reports a table without
    /// migration history.
    /// </summary>
    protected abstract Task RunBootstrapPathAsync(
        TConnection connection, TTransaction? transaction, string? schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the normal-update path: walks migrations whose version exceeds the recorded
    /// max and writes a history row per migration. Invoked when re-detection reports a table
    /// with existing migration history.
    /// </summary>
    protected abstract Task RunNormalPathAsync(
        TConnection connection, TTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken);

    /// <summary>
    /// TOCTOU re-detection under the UoW. Default implementation calls the injected
    /// detection helper (<see cref="IAmABoxMigrationDetectionHelper{TConnection,TTransaction}.DoesTableExistAsync"/>
    /// then <see cref="IAmABoxMigrationDetectionHelper{TConnection,TTransaction}.DoesHistoryExistAsync"/>,
    /// short-circuiting the history check when the table is missing). Derived classes whose
    /// backend has a different detection model can override.
    /// </summary>
    protected virtual async Task<(bool tableExists, bool historyExists)> RedetectStateAsync(
        TConnection connection, TTransaction? transaction, string? schemaName, string tableName,
        CancellationToken cancellationToken)
    {
        var tableExists = await _detectionHelper.DoesTableExistAsync(
            connection, tableName, schemaName, cancellationToken, transaction);
        var historyExists = tableExists && await _detectionHelper.DoesHistoryExistAsync(
            connection, tableName, schemaName, cancellationToken, transaction);
        return (tableExists, historyExists);
    }

    /// <summary>
    /// Rejects duplicate, gap, or out-of-order migration versions before any work begins.
    /// A malformed list corrupts every path branch (PK violation on history insert, skipped
    /// ALTERs, double-applied DDL), so the validation sits at <see cref="MigrateAsync"/> entry.
    /// Lifted from spec 0027 Items H/I/Q.
    /// </summary>
    protected static void ValidateMigrationsMonotonic(
        string? schemaName, string tableName, IReadOnlyList<IAmABoxMigration> migrations)
    {
        for (var i = 1; i < migrations.Count; i++)
        {
            var prev = migrations[i - 1].Version;
            var curr = migrations[i].Version;
            if (curr != prev + 1)
            {
                var qualified = schemaName is null ? tableName : $"{schemaName}.{tableName}";
                throw new ConfigurationException(
                    $"Migration list for '{qualified}' is not contiguous and ascending: " +
                    $"V{prev} followed by V{curr} (expected V{prev + 1}).");
            }
        }
    }
}
