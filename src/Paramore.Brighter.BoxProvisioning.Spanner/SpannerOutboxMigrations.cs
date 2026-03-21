using System.Collections.Generic;
using Paramore.Brighter.Outbox.Spanner;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

/// <summary>
/// Defines the migration history for Spanner outbox tables.
/// </summary>
public static class SpannerOutboxMigrations
{
    public static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration config)
    {
        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create outbox table",
                UpScript: SpannerOutboxBuilder.GetDDL(
                    config.OutBoxTableName,
                    config.BinaryMessagePayload))
        ];
    }
}
