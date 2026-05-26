#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

namespace Paramore.Brighter.PostgreSql;

/// <summary>
/// Quote-safe formatting for PostgreSQL identifiers (table names, schema names) that flow into
/// DDL and DML strings. Lowercases first, then double-quotes — preserving PostgreSQL's natural
/// identifier fold for unquoted input while unlocking reserved-keyword names (<c>Order</c>,
/// <c>User</c>, <c>Group</c>, ...) that fail with unquoted DDL.
/// </summary>
/// <remarks>
/// PostgreSQL case-folds unquoted identifiers to lowercase at parse time, so historically every
/// Brighter PG table created with the default mixed-case <c>"Outbox"</c> configuration value
/// has lived as <c>outbox</c> in <c>pg_class</c>. Quoting alone (<c>"Outbox"</c>) would
/// case-preserve the new name and miss the existing folded table on chain replay; lowercase-only
/// (<c>outbox</c>) still fails on reserved keywords because the unquoted keyword is rejected
/// by the parser. Lowercase-then-quote yields <c>"outbox"</c> — same name as the historical
/// folded form, but as a quoted identifier so reserved keywords parse cleanly.
/// <para>
/// Inputs must already pass <see cref="Paramore.Brighter.BoxProvisioning.Identifiers.AssertSafe"/>
/// (ASCII letters, digits, underscores). This helper does no validation — call sites validate at
/// their entry points to keep error messages close to the configured value.
/// </para>
/// <para>
/// ASCII-only inputs make <see cref="string.ToLowerInvariant"/> equivalent to PostgreSQL's
/// identifier fold; the regex in <c>Identifiers.AssertSafe</c> rejects non-ASCII characters so
/// the two folds cannot diverge in practice.
/// </para>
/// </remarks>
public static class PgIdentifier
{
    /// <summary>
    /// Returns the input identifier lowercased and double-quoted: <c>"my_table"</c>.
    /// </summary>
    /// <param name="identifier">A pre-validated SQL identifier.</param>
    public static string Quote(string identifier) =>
        $"\"{identifier.ToLowerInvariant()}\"";

    /// <summary>
    /// Returns a schema-qualified, lowercased, double-quoted identifier:
    /// <c>"schema"."table"</c>. When <paramref name="schema"/> is null, only the table is
    /// returned (no schema prefix) — callers that need a default schema must supply it.
    /// </summary>
    public static string QuoteQualified(string? schema, string table) =>
        schema is null
            ? Quote(table)
            : $"{Quote(schema)}.{Quote(table)}";

    /// <summary>
    /// Returns the input lowercased without quoting. Use this for values that must match the
    /// PG-folded form in parameterized lookups (<c>information_schema.tables</c>,
    /// <c>__BrighterMigrationHistory.BoxTableName</c>, advisory-lock key strings) — these
    /// already live in string columns or hash inputs and do not need quoting, only fold
    /// normalization so a configured <c>"Outbox"</c> matches the stored <c>outbox</c>.
    /// </summary>
    public static string Normalize(string identifier) =>
        identifier.ToLowerInvariant();
}
