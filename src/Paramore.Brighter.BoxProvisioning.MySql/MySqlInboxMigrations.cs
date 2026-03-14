using System.Collections.Generic;
using Paramore.Brighter.Inbox.MySql;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Defines the migration history for MySQL inbox tables.
/// </summary>
public static class MySqlInboxMigrations
{
    public static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration config)
    {
        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create inbox table",
                UpScript: MySqlInboxBuilder.GetDDL(
                    config.InBoxTableName,
                    config.BinaryMessagePayload))
        ];
    }
}
