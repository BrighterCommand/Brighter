namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

/// <summary>
/// Backend-specific latest migration versions used by tests to parameterise
/// <c>MigrationVersion</c> assertions instead of hard-coding literals. Spanner's runner is
/// degenerate (fresh-install only) — once Phase 5 lands, the runner stamps these values
/// directly without walking a chain.
/// </summary>
internal static class ExpectedMigrationVersions
{
    public const int OutboxLatest = 7;
    public const int InboxLatest = 3;
}
