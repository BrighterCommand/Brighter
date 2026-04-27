using System.Collections.Generic;
using Paramore.Brighter.Outbox.MySql;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Defines the migration history for MySQL outbox tables.
/// </summary>
public static class MySqlOutboxMigrations
{
    public static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration config)
    {
        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create outbox table",
                UpScript: MySqlOutboxBuilder.GetDDL(
                    config.OutBoxTableName,
                    config.BinaryMessagePayload),
                LogicalColumns: new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
                {
                    "MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"
                })
        ];
    }
}
