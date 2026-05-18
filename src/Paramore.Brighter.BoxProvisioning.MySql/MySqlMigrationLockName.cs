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

using System;
using System.Security.Cryptography;
using System.Text;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Builds the session-scoped lock identifier passed to MySQL <c>GET_LOCK</c> /
/// <c>RELEASE_LOCK</c> for a given (schema, table) pair.
/// </summary>
/// <remarks>
/// MySQL <c>GET_LOCK(name, timeout)</c> requires names to be at most 64 characters; from
/// MySQL 5.7.5 onward longer names raise <c>ER_USER_LOCK_WRONG_NAME</c>, and earlier versions
/// silently truncated the name (which would let two distinct tables share a lock and let one
/// runner skip a lock another already held).
/// <para>
/// The schema is folded into the lock name so two same-named tables in different schemas
/// (e.g. <c>BrighterTests.Outbox</c> and <c>billing.Outbox</c>) acquire distinct advisory locks
/// instead of serialising on a shared key. Matches the MSSQL runner's <c>lockResource</c> shape
/// at <c>MsSqlBoxMigrationRunner.cs:90</c>.
/// </para>
/// <para>
/// For composites that fit the simple form (<c>BrighterMigration_&lt;schema&gt;.&lt;table&gt;</c> ≤ 64 chars,
/// or <c>BrighterMigration_&lt;table&gt;</c> when schema is null/empty) the form is preserved
/// for diagnostic readability. For longer composites a SHA-256 suffix is folded in over the
/// full <c>schema.table</c> input to keep the result within the limit while remaining
/// collision-resistant across distinct (schema, table) pairs that share a long common prefix.
/// </para>
/// <para>
/// Hash truncation: the suffix is the first 8 bytes (16 hex characters) of the SHA-256 digest.
/// Birthday-bound collision probability is ~1 in 2^32 (~4 billion) over distinct
/// (schema, table) pairs that share the same long prefix — accepted as negligible given the
/// per-deployment population is typically &lt; 100 box tables, and any collision merely
/// serialises two migrations on a shared advisory lock (correctness preserved, only the
/// concurrency boundary widens). Full SHA-256 will not fit inside the 64-char GET_LOCK limit
/// after the diagnostic prefix.
/// </para>
/// </remarks>
public static class MySqlMigrationLockName
{
    private const string Prefix = "BrighterMigration_";
    private const int MySqlGetLockNameLimit = 64;
    private const int HashHexChars = 16;
    // Long-form layout: Prefix(18) + truncatedPrefix(N) + '_'(1) + hashHex(16) ≤ 64 → N ≤ 29.
    private const int LongFormPrefixBudget = 29;

    /// <summary>
    /// Builds the lock name for the given <paramref name="schema"/> and <paramref name="tableName"/>,
    /// guaranteed not to exceed MySQL's 64-character <c>GET_LOCK</c> limit and collision-resistant
    /// across distinct (schema, table) pairs that share a long common prefix.
    /// </summary>
    /// <param name="schema">The schema (database) the table lives in. When null or empty the
    /// schema is omitted from the composite — the runner always passes a non-empty value
    /// resolved via <c>SELECT DATABASE()</c> when no explicit schema is configured, so this
    /// fallback is for direct callers of the helper.</param>
    /// <param name="tableName">The box table name.</param>
    /// <returns>A lock identifier suitable for <c>GET_LOCK</c>.</returns>
    public static string For(string? schema, string tableName)
    {
        var composite = string.IsNullOrEmpty(schema)
            ? tableName
            : $"{schema}.{tableName}";

        var simpleForm = Prefix + composite;
        if (simpleForm.Length <= MySqlGetLockNameLimit)
        {
            return simpleForm;
        }

        var hashSuffix = ShortHashOf(composite);
        var truncatedPrefix = composite.Substring(0, LongFormPrefixBudget);
        return $"{Prefix}{truncatedPrefix}_{hashSuffix}";
    }

    private static string ShortHashOf(string composite)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(composite));
        return Convert.ToHexString(hashBytes, 0, HashHexChars / 2).ToLowerInvariant();
    }
}
