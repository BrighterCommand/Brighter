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
    /// <param name="migrations">The ordered list of migrations to apply.</param>
    /// <param name="tableState">The current state of the box table, as detected by the provisioner.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MigrateAsync(
        string tableName,
        string? schemaName,
        IReadOnlyList<IAmABoxMigration> migrations,
        BoxTableState tableState,
        CancellationToken cancellationToken = default);
}
