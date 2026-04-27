using System.Collections.Generic;
using Paramore.Brighter.Inbox.Sqlite;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Defines the migration history for SQLite inbox tables.
/// </summary>
public static class SqliteInboxMigrations
{
    public static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration config)
    {
        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create inbox table",
                UpScript: SqliteInboxBuilder.GetDDL(
                    config.InBoxTableName,
                    config.BinaryMessagePayload),
                LogicalColumns: new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
                {
                    "CommandId", "CommandType", "CommandBody", "Timestamp"
                })
        ];
    }
}
