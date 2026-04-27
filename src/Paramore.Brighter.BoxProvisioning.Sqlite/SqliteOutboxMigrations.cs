using System.Collections.Generic;
using Paramore.Brighter.Outbox.Sqlite;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Defines the migration history for SQLite outbox tables.
/// </summary>
public static class SqliteOutboxMigrations
{
    public static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration config)
    {
        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create outbox table",
                UpScript: SqliteOutboxBuilder.GetDDL(
                    config.OutBoxTableName,
                    config.BinaryMessagePayload),
                LogicalColumns: new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
                {
                    "MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"
                })
        ];
    }
}
