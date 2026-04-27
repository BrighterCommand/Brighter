using System.Collections.Generic;
using Paramore.Brighter.Inbox.MsSql;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Defines the migration history for MSSQL inbox tables.
/// </summary>
public static class MsSqlInboxMigrations
{
    /// <summary>
    /// Returns all migrations for the MSSQL inbox, ordered by version.
    /// </summary>
    /// <param name="config">The relational database configuration.</param>
    /// <returns>An ordered list of migrations.</returns>
    public static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration config)
    {
        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create inbox table",
                UpScript: SqlInboxBuilder.GetDDL(
                    config.InBoxTableName,
                    config.BinaryMessagePayload),
                LogicalColumns: new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
                {
                    "CommandId", "CommandType", "CommandBody", "Timestamp"
                })
        ];
    }
}
