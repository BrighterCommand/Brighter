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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

// Bridging shim — Phase 2.4 of spec 0028. Pure delegation onto a singleton
// SqliteBoxDetectionHelper instance, passing null for the new schemaName slot
// (SQLite has no schema concept; the parameter is accepted and ignored).
// Removed in Phase 8 when call-sites rewire to instance dispatch.
public static class SqliteBoxDetectionHelpers
{
    private static readonly SqliteBoxDetectionHelper s_instance = new();

    public static Task<bool> DoesTableExistAsync(
        SqliteConnection connection, string tableName,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
        => s_instance.DoesTableExistAsync(
            connection, tableName, null, cancellationToken, transaction);

    public static Task<bool> DoesHistoryExistAsync(
        SqliteConnection connection, string tableName,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
        => s_instance.DoesHistoryExistAsync(
            connection, tableName, null, cancellationToken, transaction);

    public static Task<int> GetMaxVersionAsync(
        SqliteConnection connection, string tableName,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
        => s_instance.GetMaxVersionAsync(
            connection, tableName, null, cancellationToken, transaction);

    public static Task<HashSet<string>> GetTableColumnsAsync(
        SqliteConnection connection, string tableName,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
        => s_instance.GetTableColumnsAsHashSetAsync(
            connection, tableName, null, cancellationToken, transaction);

    public static Task<int> DetectCurrentVersionAsync(
        SqliteConnection connection, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
        => s_instance.DetectCurrentVersionAsync(
            connection, tableName, null, boxType, migrations, cancellationToken, transaction);

    public static string DiscriminatorFor(BoxType boxType)
        => s_instance.DiscriminatorFor(boxType);
}
