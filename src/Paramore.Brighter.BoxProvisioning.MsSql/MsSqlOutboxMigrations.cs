using System.Collections.Generic;
using Paramore.Brighter.Outbox.MsSql;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Defines the migration history for MSSQL outbox tables.
/// </summary>
public static class MsSqlOutboxMigrations
{
    /// <summary>
    /// Returns all migrations for the MSSQL outbox, ordered by version.
    /// </summary>
    /// <param name="config">The relational database configuration.</param>
    /// <returns>An ordered list of migrations.</returns>
    public static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration config)
    {
        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create outbox table",
                UpScript: SqlOutboxBuilder.GetDDL(
                    config.OutBoxTableName,
                    config.BinaryMessagePayload))
        ];
    }
}
