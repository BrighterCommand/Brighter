namespace Paramore.Brighter.BoxProvisioning.Tests.Drift;

/// <summary>
/// Identifies the per-backend identifier-quoting grammar that
/// <see cref="DdlColumnExtractor.GetExpectedColumns(string, QuoteStyle)"/> should use when
/// parsing a <c>CREATE TABLE</c> statement. Drives both the leading-identifier regex on each
/// column-declaration line and the case-sensitivity of the returned <see cref="System.Collections.Generic.HashSet{T}"/>.
/// </summary>
public enum QuoteStyle
{
    /// <summary>SQL Server: identifiers wrapped in <c>[brackets]</c>; case-insensitive.</summary>
    MsSql,

    /// <summary>PostgreSQL: identifiers wrapped in <c>"double quotes"</c> or written unquoted (folded to lowercase by the engine); case-sensitive.</summary>
    Postgres,

    /// <summary>MySQL / MariaDB: identifiers wrapped in <c>`backticks`</c>; case-insensitive.</summary>
    MySql,

    /// <summary>SQLite: identifiers wrapped in <c>[brackets]</c> or <c>"double quotes"</c>; case-insensitive.</summary>
    Sqlite,

    /// <summary>Spanner (GoogleSQL dialect): identifiers wrapped in <c>`backticks`</c>; case-insensitive.
    /// Wire-equivalent to <see cref="MySql"/> today, but kept as a distinct enum value so the
    /// Spanner drift test reads as Spanner-specific and so a future GoogleSQL-only quoting quirk
    /// has a place to live without touching MySQL extraction.</summary>
    Spanner
}
