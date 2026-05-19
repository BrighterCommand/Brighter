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
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Inbox.MsSql;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Defines the migration history for MSSQL inbox tables.
/// </summary>
/// <remarks>
/// V1 is the fresh-install baseline whose <c>UpScript</c> is the live <see cref="SqlInboxBuilder"/>
/// DDL (per ADR 0057 §3 fresh-install fast path). V2 adds <c>ContextKey</c> as a conditional
/// <c>ALTER TABLE ADD</c> guarded by <c>IF COL_LENGTH(...) IS NULL</c> per ADR 0057 §5, so it is
/// idempotent and safe to re-execute. The accumulated <c>LogicalColumns</c> across V1..V2 plus
/// the MSSQL inbox housekeeping <c>Id</c> column equals the live builder's column set — verified
/// by the drift test in <c>tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning</c>.
/// </remarks>
public class MsSqlInboxMigrationCatalog : IAmABoxMigrationCatalog
{
    private const string DefaultSchema = "dbo";

    private static readonly string[] s_v1Columns =
        ["CommandId", "CommandType", "CommandBody", "Timestamp"];

    private static readonly string[] s_v2AddedColumns = ["ContextKey"];

    /// <inheritdoc />
    public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
    {
        Identifiers.AssertSafe(
            configuration.InBoxTableName,
            nameof(IAmARelationalDatabaseConfiguration.InBoxTableName));
        return SqlInboxBuilder.GetDDL(configuration.InBoxTableName, configuration.BinaryMessagePayload);
    }

    /// <summary>
    /// Returns all migrations for the MSSQL inbox, ordered by version.
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    /// <returns>An ordered list of migrations from V1 to V2.</returns>
    public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
    {
        var schema = configuration.SchemaName ?? DefaultSchema;
        var table = configuration.InBoxTableName;

        Identifiers.AssertSafe(table, nameof(IAmARelationalDatabaseConfiguration.InBoxTableName));
        Identifiers.AssertSafe(schema, nameof(IAmARelationalDatabaseConfiguration.SchemaName));

        return
        [
            new BoxMigration(
                Version: 1,
                Description: "Create inbox table",
                UpScript: SqlInboxBuilder.GetDDL(table, configuration.BinaryMessagePayload),
                LogicalColumns: Cumulative(1)),

            new BoxMigration(
                Version: 2,
                Description: "Add ContextKey column",
                UpScript: AddColumns(schema, table, ("ContextKey", "NVARCHAR(256)")),
                LogicalColumns: Cumulative(2),
                SourceReference: "787c31c52")
        ];
    }

    private static IReadOnlyCollection<string> Cumulative(int upToVersion)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (upToVersion >= 1) { set.UnionWith(s_v1Columns); }
        if (upToVersion >= 2) { set.UnionWith(s_v2AddedColumns); }
        return set;
    }

    private static string AddColumns(string schema, string table, params (string Column, string Type)[] columns) =>
        string.Join(Environment.NewLine, columns.Select(c => AddColumn(schema, table, c.Column, c.Type)));

    private static string AddColumn(string schema, string table, string column, string type) =>
        $"IF COL_LENGTH(N'[{schema}].[{table}]', N'{column}') IS NULL{Environment.NewLine}" +
        $"    ALTER TABLE [{schema}].[{table}] ADD [{column}] {type} NULL;";
}
