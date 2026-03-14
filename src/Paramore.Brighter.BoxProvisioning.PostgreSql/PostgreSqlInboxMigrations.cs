using System.Collections.Generic;
using Paramore.Brighter.Inbox.Postgres;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Defines the migration history for PostgreSQL inbox tables.
/// </summary>
public static class PostgreSqlInboxMigrations
{
    public static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration config)
    {
        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create inbox table",
                UpScript: PostgreSqlInboxBuilder.GetDDL(
                    config.InBoxTableName,
                    config.BinaryMessagePayload))
        ];
    }
}
