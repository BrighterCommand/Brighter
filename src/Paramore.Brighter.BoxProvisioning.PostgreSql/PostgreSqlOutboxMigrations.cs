using System.Collections.Generic;
using Paramore.Brighter.Outbox.PostgreSql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Defines the migration history for PostgreSQL outbox tables.
/// </summary>
public static class PostgreSqlOutboxMigrations
{
    public static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration config)
    {
        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create outbox table",
                UpScript: PostgreSqlOutboxBuilder.GetDDL(
                    config.OutBoxTableName,
                    config.BinaryMessagePayload),
                LogicalColumns: new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
                {
                    "MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"
                })
        ];
    }
}
