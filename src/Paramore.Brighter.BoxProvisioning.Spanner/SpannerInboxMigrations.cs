using System.Collections.Generic;
using Paramore.Brighter.Inbox.Spanner;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

/// <summary>
/// Defines the migration history for Spanner inbox tables.
/// </summary>
public static class SpannerInboxMigrations
{
    public static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration config)
    {
        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create inbox table",
                UpScript: SpannerInboxBuilder.GetDDL(config.InBoxTableName))
        ];
    }
}
