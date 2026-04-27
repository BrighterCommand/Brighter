using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Knows how to run migration steps against a database and track which
/// versions have been applied.
/// </summary>
public interface IAmABoxMigrationRunner
{
    /// <summary>
    /// Apply all outstanding migrations for the specified box table.
    /// </summary>
    /// <param name="tableName">The box table name.</param>
    /// <param name="schemaName">The database schema name (e.g. "dbo" for MSSQL).</param>
    /// <param name="boxType">The type of box being migrated; used to pick the discriminator column for under-lock re-detection.</param>
    /// <param name="migrations">The ordered list of migrations to apply.</param>
    /// <param name="tableState">The pre-lock state of the box table, treated as a hint only — the runner re-reads state under the lock to defeat TOCTOU races.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MigrateAsync(
        string tableName,
        string? schemaName,
        BoxType boxType,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default);
}
