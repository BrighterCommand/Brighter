namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

/// <summary>
/// Backend-specific latest migration versions used by tests to parameterise
/// <c>MigrationVersion</c> assertions instead of hard-coding literals. Keeps the test suite
/// stable as the migration chain grows in Phases 1–4.
/// </summary>
internal static class ExpectedMigrationVersions
{
    public const int OutboxLatest = 8;
    public const int InboxLatest = 3;
}
