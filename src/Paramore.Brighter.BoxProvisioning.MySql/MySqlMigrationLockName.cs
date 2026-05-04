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
/// <c>RELEASE_LOCK</c> for a given box table.
/// </summary>
/// <remarks>
/// MySQL <c>GET_LOCK(name, timeout)</c> requires <paramref name="tableName"/>-derived names to
/// be at most 64 characters; from MySQL 5.7.5 onward longer names raise
/// <c>ER_USER_LOCK_WRONG_NAME</c>, and earlier versions silently truncated the name (which would
/// let two distinct tables share a lock and let one runner skip a lock another already held).
/// <para>
/// For names that fit the simple form (<c>BrighterMigration_</c> + table name ≤ 64 chars) the
/// historical format is preserved so a running deployment that holds a lock under the old name
/// continues to interlock with the new code. For longer names a SHA-256 suffix is folded in to
/// keep the result within the limit while remaining collision-resistant across distinct tables
/// that share a long common prefix.
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
    /// Builds the lock name for <paramref name="tableName"/>, guaranteed not to exceed
    /// MySQL's 64-character <c>GET_LOCK</c> limit and collision-resistant across distinct
    /// table names that share a long common prefix.
    /// </summary>
    /// <param name="tableName">The fully-qualified box table name.</param>
    /// <returns>A lock identifier suitable for <c>GET_LOCK</c>.</returns>
    public static string For(string tableName)
    {
        var simpleForm = Prefix + tableName;
        if (simpleForm.Length <= MySqlGetLockNameLimit)
        {
            return simpleForm;
        }

        var hashSuffix = ShortHashOf(tableName);
        var truncatedPrefix = tableName.Substring(0, LongFormPrefixBudget);
        return $"{Prefix}{truncatedPrefix}_{hashSuffix}";
    }

    private static string ShortHashOf(string tableName)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(tableName));
        return Convert.ToHexString(hashBytes, 0, HashHexChars / 2).ToLowerInvariant();
    }
}
