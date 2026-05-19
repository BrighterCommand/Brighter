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
/// V1's <c>UpScript</c> is the literal historical baseline DDL — the first MSSQL inbox
/// builder shape (commit <c>b7f96957b</c>, March 2019). Spec 0027 R1 split "live builder
/// DDL" away from V1.UpScript: the fresh-install fast path (ADR 0057 §3) now sources its
/// DDL from <see cref="FreshInstallDdl"/>, so V1.UpScript is free to carry the honest
/// historical shape. V2 adds <c>ContextKey</c> as a conditional <c>ALTER TABLE ADD</c>
/// guarded by <c>IF COL_LENGTH(...) IS NULL</c> per ADR 0057 §5.
/// <para>
/// Born-past-V1 asymmetry: the MSSQL inbox first shipped <em>with</em> <c>ContextKey</c> —
/// there is no pre-ContextKey MSSQL inbox in the wild. V1.UpScript therefore creates a
/// table whose physical column set is the union of V1.LogicalColumns + V2's additions
/// (plus the <c>Id</c> housekeeping column). On chain replay V2's idempotency guard sees
/// the existing column and skips the ALTER. V1.LogicalColumns remains the "logical
/// pre-V2" set (no ContextKey) so the bootstrap-detection contract (ADR 0057 §4) can
/// still distinguish a hypothetical pre-V2 table.
/// </para>
/// <para>
/// The accumulated <c>LogicalColumns</c> across V1..V2 plus the MSSQL inbox housekeeping
/// <c>Id</c> column equals the live builder's column set — verified by the drift test in
/// <c>tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning</c>.
/// </para>
/// </remarks>
public class MsSqlInboxMigrationCatalog : IAmABoxMigrationCatalog
{
    private const string DefaultSchema = "dbo";

    private static readonly string[] s_v1Columns =
        ["CommandId", "CommandType", "CommandBody", "Timestamp"];

    private static readonly string[] s_v2AddedColumns = ["ContextKey"];

    // Literal historical MSSQL inbox DDL extracted from commit b7f96957b (March 2019). The
    // table first shipped with ContextKey already present — see the born-past-V1 note in the
    // class remarks. {0} = table name (validated).
    private const string V1HistoricalDdl =
        """
        CREATE TABLE {0}
            (
                [Id] [BIGINT] IDENTITY(1, 1) NOT NULL ,
                [CommandId] [UNIQUEIDENTIFIER] NOT NULL ,
                [CommandType] [NVARCHAR](256) NULL ,
                [CommandBody] [NTEXT] NULL ,
                [Timestamp] [DATETIME] NULL ,
                [ContextKey] [NVARCHAR](256) NULL,
                PRIMARY KEY ( [Id] )
            );
        """;

    /// <inheritdoc />
    public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
    {
        Identifiers.AssertSafe(
            configuration.InBoxTableName,
            nameof(IAmARelationalDatabaseConfiguration.InBoxTableName));
        // Pass SchemaName so the builder schema-qualifies the CREATE TABLE — otherwise the
        // table lands in the connection's default schema (typically [dbo]) regardless of
        // SchemaName. Per PR #4039 reviewer item M4-1 (F1a). See the outbox catalog for
        // the full rationale; the same fix applies symmetrically here.
        if (configuration.SchemaName is not null)
        {
            Identifiers.AssertSafe(
                configuration.SchemaName,
                nameof(IAmARelationalDatabaseConfiguration.SchemaName));
        }
        return SqlInboxBuilder.GetDDL(
            configuration.InBoxTableName,
            configuration.BinaryMessagePayload,
            configuration.SchemaName);
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
                UpScript: string.Format(V1HistoricalDdl, table),
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
