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
                UpScript: SpannerInboxBuilder.GetDDL(config.InBoxTableName),
                // TODO(spec-0027 Phase 5): file deleted — runner no longer uses migration list.
                // Empty-set bridge keeps the build green between Phase 0 (interface extension) and
                // Phase 5 (Spanner degenerate runner rework, which deletes this file entirely).
                LogicalColumns: new HashSet<string>())
        ];
    }
}
