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

using System.Data.Common;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Attaches the outbox's own, physically separate Sqlite database file to a connection under the fixed
/// alias <c>outboxdb</c> (AC-52), so <see cref="DepositTransactionWebApplicationFactory.AttachedOutboxTableReference"/>
/// resolves on it. Idempotent - checks <c>pragma_database_list</c> first, so attaching an already-attached
/// connection is a no-op.
/// </summary>
/// <remarks>
/// The outbox table lives in its own file, never attached to the entity table's own connection, so that
/// an <see cref="ScopeAffinity.AlwaysNew"/> handler's independent outbox write never contends with a
/// caller's still-open transaction on the entity table - Sqlite's write lock is per physical database
/// file, and a connection with a second file attached is defensively locked across both files for the
/// whole of any write transaction it holds, even if that transaction never touches the attached file.
/// </remarks>
internal static class OutboxDatabaseAttachment
{
    public static void EnsureAttached(DbConnection connection, string outboxDatabasePath)
    {
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM pragma_database_list WHERE name = 'outboxdb'";
            if (System.Convert.ToInt64(check.ExecuteScalar()) > 0)
                return;
        }

        using var attach = connection.CreateCommand();
        attach.CommandText = $"ATTACH DATABASE '{outboxDatabasePath}' AS outboxdb;";
        attach.ExecuteNonQuery();
    }
}
