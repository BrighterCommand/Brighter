namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

/// <summary>
/// Backend-specific latest migration versions used by tests to parameterise
/// <c>MigrationVersion</c> assertions instead of hard-coding literals. Keeps the test suite
/// stable as the migration chain grows in Phases 1–4.
/// </summary>
/// <remarks>
/// PostgreSQL inbox was born with <c>ContextKey</c>, so the V2 migration that adds
/// <c>ContextKey</c> to other backends has no analogue here — its V1 baseline already carries it.
/// Spec 0027 (#2541) adds a <c>CausationId</c> column to every relational inbox, so PostgreSQL
/// inbox now gains a V2 of its own (other backends advance V2→V3), leaving its latest at 2.
/// </remarks>
internal static class ExpectedMigrationVersions
{
    public const int OutboxLatest = 7;
    public const int InboxLatest = 2;
}
