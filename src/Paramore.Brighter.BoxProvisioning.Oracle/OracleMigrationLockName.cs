// The MIT License (MIT)
// Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Security.Cryptography;
using System.Text;

namespace Paramore.Brighter.BoxProvisioning.Oracle;

/// <summary>
/// Builds the advisory-lock identifier passed to <c>DBMS_LOCK.ALLOCATE_UNIQUE</c> for a
/// given (schema, table) pair.
/// </summary>
/// <remarks>
/// <c>DBMS_LOCK.ALLOCATE_UNIQUE</c> supports lock names up to 128 characters.
/// The schema is folded into the lock name so two same-named tables in different schemas
/// acquire distinct advisory locks.
/// <para>
/// For composites that fit the simple form
/// (<c>BrighterMigration_&lt;schema&gt;.&lt;table&gt;</c> ≤ 128 chars, or
/// <c>BrighterMigration_&lt;table&gt;</c> when schema is null/empty) the form is preserved
/// for diagnostic readability. For longer composites a SHA-256 suffix is folded in to stay
/// within the limit while remaining collision-resistant.
/// </para>
/// <para>
/// Hash truncation: the suffix is the first 8 bytes (16 hex characters) of the SHA-256
/// digest. Birthday-bound collision probability is ~1 in 2^32 over distinct (schema, table)
/// pairs — accepted as negligible for typical per-deployment box-table counts.
/// </para>
/// </remarks>
public static class OracleMigrationLockName
{
    private const string Prefix = "BrighterMigration_";
    private const int DbmsLockNameLimit = 128;
    private const int HashHexChars = 16;
    // Long-form layout: Prefix(18) + truncatedPrefix(N) + '_'(1) + hashHex(16) ≤ 128 → N ≤ 93.
    private const int LongFormPrefixBudget = 93;

    /// <summary>
    /// Builds the lock name for the given <paramref name="schema"/> and <paramref name="tableName"/>,
    /// guaranteed not to exceed the 128-character <c>DBMS_LOCK.ALLOCATE_UNIQUE</c> limit.
    /// </summary>
    /// <param name="schema">The Oracle schema the table lives in. When null or empty the schema
    /// is omitted; the runner always passes the resolved current schema.</param>
    /// <param name="tableName">The box table name.</param>
    /// <returns>A lock identifier suitable for <c>DBMS_LOCK.ALLOCATE_UNIQUE</c>.</returns>
    public static string For(string? schema, string tableName)
    {
        var composite = string.IsNullOrEmpty(schema)
            ? tableName
            : $"{schema}.{tableName}";

        var simpleForm = Prefix + composite;
        if (simpleForm.Length <= DbmsLockNameLimit)
            return simpleForm;

        var hashSuffix = ShortHashOf(composite);
        var truncatedPrefix = composite.Substring(0, LongFormPrefixBudget);
        return $"{Prefix}{truncatedPrefix}_{hashSuffix}";
    }

    private static string ShortHashOf(string composite)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(composite));

        var builder = new StringBuilder(HashHexChars);
        for (var i = 0; i < HashHexChars / 2; i++)
        {
            builder.Append(hashBytes[i].ToString("x2"));
        }

        return builder.ToString();
    }
}
