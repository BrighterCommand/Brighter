using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Paramore.Brighter.BoxProvisioning.Tests.Drift;

/// <summary>
/// Test-project helper that parses a <c>CREATE TABLE</c> string and returns the set of column
/// identifiers declared in its body. Used by per-backend drift tests in Phases 1–4 to verify
/// that the migration chain's <c>LogicalColumns</c> + housekeeping union matches the live
/// builder DDL — catching the case where a developer adds a column to the builder but forgets
/// to add a corresponding migration.
/// </summary>
/// <remarks>
/// This is not a SQL parser; it is a syntactic scanner tuned to the DDL shapes produced by
/// the four <c>*OutboxBuilder</c> / <c>*InboxBuilder</c> classes. It assumes:
/// <list type="bullet">
///   <item>The first <c>(</c> in the DDL opens the table body, and parens nest cleanly.</item>
///   <item>Top-level commas separate column declarations and table-level constraints.</item>
///   <item>Each column-declaration line begins with an identifier, optionally quoted per backend.</item>
///   <item>Lines that begin with <c>CONSTRAINT</c> / <c>PRIMARY KEY</c> / <c>FOREIGN KEY</c> /
///     <c>UNIQUE</c> / <c>INDEX</c> / <c>KEY</c> / <c>FULLTEXT</c> / <c>SPATIAL</c> / <c>CHECK</c>
///     are table-level constraints and are skipped.</item>
/// </list>
/// Inline <c>COLLATE NOCASE</c> (or any other trailing modifier) on a column-declaration line
/// is harmless because the scanner only reads the leading identifier and ignores the rest of
/// the line.
/// </remarks>
public static class DdlColumnExtractor
{
    private static readonly string[] s_reservedClauseKeywords =
    [
        "CONSTRAINT",
        "PRIMARY",
        "FOREIGN",
        "UNIQUE",
        "INDEX",
        "KEY",
        "FULLTEXT",
        "SPATIAL",
        "CHECK"
    ];

    private static readonly Regex s_bracketed = new(@"^\[([^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex s_backticked = new(@"^`([^`]+)`", RegexOptions.Compiled);
    private static readonly Regex s_doubleQuoted = new(@"^""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex s_unquoted = new(@"^([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    /// <summary>
    /// Parses <paramref name="ddl"/> and returns the set of column identifiers declared in
    /// its first <c>CREATE TABLE</c> body, using the quoting and case-sensitivity conventions
    /// of <paramref name="quoteStyle"/>.
    /// </summary>
    /// <param name="ddl">A SQL <c>CREATE TABLE</c> statement; only the first table body is read.</param>
    /// <param name="quoteStyle">The backend whose identifier-quoting grammar to use.</param>
    /// <returns>
    /// A <see cref="HashSet{T}"/> of column names. The comparer is
    /// <see cref="StringComparer.Ordinal"/> for <see cref="QuoteStyle.Postgres"/> (which is
    /// case-sensitive) and <see cref="StringComparer.OrdinalIgnoreCase"/> otherwise.
    /// Returns an empty set if no table body can be located.
    /// </returns>
    public static HashSet<string> GetExpectedColumns(string ddl, QuoteStyle quoteStyle)
    {
        var comparer = quoteStyle == QuoteStyle.Postgres
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;
        var columns = new HashSet<string>(comparer);

        var body = ExtractTableBody(ddl);
        if (body is null)
        {
            return columns;
        }

        foreach (var line in SplitOnTopLevelCommas(body))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }
            if (StartsWithReservedClauseKeyword(trimmed))
            {
                continue;
            }

            var name = ExtractLeadingIdentifier(trimmed, quoteStyle);
            if (name is not null)
            {
                columns.Add(name);
            }
        }

        return columns;
    }

    private static string? ExtractTableBody(string ddl)
    {
        var openIdx = ddl.IndexOf('(');
        if (openIdx < 0)
        {
            return null;
        }

        var depth = 0;
        for (var i = openIdx; i < ddl.Length; i++)
        {
            switch (ddl[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth == 0)
                    {
                        return ddl.Substring(openIdx + 1, i - openIdx - 1);
                    }
                    break;
            }
        }
        return null;
    }

    private static IEnumerable<string> SplitOnTopLevelCommas(string body)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < body.Length; i++)
        {
            switch (body[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    yield return body.Substring(start, i - start);
                    start = i + 1;
                    break;
            }
        }
        if (start < body.Length)
        {
            yield return body[start..];
        }
    }

    private static bool StartsWithReservedClauseKeyword(string trimmed)
    {
        foreach (var keyword in s_reservedClauseKeywords)
        {
            if (trimmed.StartsWith(keyword, StringComparison.OrdinalIgnoreCase)
                && (trimmed.Length == keyword.Length || IsKeywordBoundary(trimmed[keyword.Length])))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsKeywordBoundary(char c) => char.IsWhiteSpace(c) || c == '(';

    private static string? ExtractLeadingIdentifier(string trimmed, QuoteStyle quoteStyle)
    {
        return quoteStyle switch
        {
            QuoteStyle.MsSql => TryMatch(s_bracketed, trimmed) ?? TryMatch(s_unquoted, trimmed),
            QuoteStyle.Sqlite => TryMatch(s_bracketed, trimmed)
                                 ?? TryMatch(s_doubleQuoted, trimmed)
                                 ?? TryMatch(s_unquoted, trimmed),
            QuoteStyle.MySql => TryMatch(s_backticked, trimmed) ?? TryMatch(s_unquoted, trimmed),
            QuoteStyle.Spanner => TryMatch(s_backticked, trimmed) ?? TryMatch(s_unquoted, trimmed),
            QuoteStyle.Postgres => TryMatch(s_doubleQuoted, trimmed) ?? TryMatch(s_unquoted, trimmed),
            _ => null
        };
    }

    private static string? TryMatch(Regex regex, string input)
    {
        var m = regex.Match(input);
        return m.Success ? m.Groups[1].Value : null;
    }
}
