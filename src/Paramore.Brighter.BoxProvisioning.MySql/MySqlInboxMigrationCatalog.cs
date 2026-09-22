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
using Paramore.Brighter.Inbox.MySql;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Defines the migration history for MySQL inbox tables.
/// </summary>
/// <remarks>
/// V1's <c>UpScript</c> is the literal historical baseline DDL — the first MySQL inbox
/// builder shape (commit <c>b7f96957b</c>, March 2019). Spec 0027 R1 split "live builder
/// DDL" away from V1.UpScript: the fresh-install fast path (ADR 0057 §3) now sources its
/// DDL from <see cref="FreshInstallDdl"/>, so V1.UpScript is free to carry the honest
/// historical shape. V2 adds <c>ContextKey</c> via the MySQL <c>information_schema.columns</c>
/// + prepared-statement idempotency pattern from ADR 0057 §5.
/// <para>
/// Born-past-V1 asymmetry: the MySQL inbox first shipped <em>with</em> <c>ContextKey</c>
/// already present — there is no pre-ContextKey MySQL inbox in the wild despite the
/// October-2018 spec version stamp on V2 (the catch-up exists for cross-backend
/// consistency, not because pre-ContextKey rows ever shipped on MySQL). V1.UpScript
/// therefore creates a table whose physical column set already includes <c>ContextKey</c>;
/// V2's runtime idempotency probe sees the column and short-circuits to <c>SELECT 1</c> on
/// chain replay. V1.LogicalColumns remains the "logical pre-V2" set (no <c>ContextKey</c>)
/// so the bootstrap-detection contract (ADR 0057 §4) can still distinguish a hypothetical
/// pre-V2 table.
/// </para>
/// <para>
/// Note that the per-version <see cref="IAmABoxMigration.UpScript"/> and
/// <see cref="IAmABoxMigration.LogicalColumns"/> play different roles after Spec 0027 R1:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="IAmABoxMigration.UpScript"/> on V1 is the historical
/// pre-V_latest DDL — used by the bootstrap and normal-update branches, NOT by the
/// fresh-install fast path. V_k's UpScript for k &gt; 1 is the incremental ALTER that
/// takes V_{k-1} to V_k.</description></item>
/// <item><description><see cref="IAmABoxMigration.LogicalColumns"/> on V1 reflects the
/// "logical pre-V2" 4-column shape (no <c>ContextKey</c>); it is the detection contract
/// used by the bootstrap branch to infer which V_k a legacy table sits at.</description></item>
/// </list>
/// <para>
/// LogicalColumns are PascalCase with <see cref="StringComparer.OrdinalIgnoreCase"/> — MySQL
/// identifiers are case-insensitive on lookup. Comparer mirrors
/// <see cref="MySqlOutboxMigrationCatalog"/>.
/// </para>
/// </remarks>
public class MySqlInboxMigrationCatalog : IAmABoxMigrationCatalog
{
    private static readonly string[] s_v1Columns =
        ["CommandId", "CommandType", "CommandBody", "Timestamp"];

    private static readonly string[] s_v2AddedColumns = ["ContextKey"];

    private static readonly string[] s_v3AddedColumns = ["CausationId"];

    // Literal historical MySQL inbox DDL extracted from commit b7f96957b (March 2019). The
    // table first shipped with ContextKey already present — see the born-past-V1 note in
    // the class remarks. {0} = table name (validated).
    // The table identifier is backtick-quoted so legal-but-reserved MySQL keyword names
    // (User, Order, Group, …) bootstrap correctly — V2 already backtick-quotes, so V1
    // is the only asymmetric step. 
    private const string V1HistoricalDdl =
        """
        CREATE TABLE `{0}`
            (
                `CommandId` CHAR(36) NOT NULL ,
                `CommandType` VARCHAR(256) NOT NULL ,
                `CommandBody` TEXT NOT NULL ,
                `Timestamp` TIMESTAMP(4) NOT NULL ,
                `ContextKey` VARCHAR(256)  NULL ,
                PRIMARY KEY (`CommandId`)
            ) ENGINE = InnoDB;
        """;

    /// <inheritdoc />
    public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
    {
        Identifiers.AssertSafe(
            configuration.InBoxTableName,
            nameof(IAmARelationalDatabaseConfiguration.InBoxTableName));
        // Pass SchemaName so the builder schema-qualifies the CREATE TABLE. 
        if (configuration.SchemaName is not null)
        {
            Identifiers.AssertSafe(
                configuration.SchemaName,
                nameof(IAmARelationalDatabaseConfiguration.SchemaName));
        }
        return MySqlInboxBuilder.GetDDL(
            configuration.InBoxTableName,
            configuration.BinaryMessagePayload,
            jsonMessage: false,
            schemaName: configuration.SchemaName);
    }

    /// <summary>
    /// Returns all migrations for the MySQL inbox, ordered by version.
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    /// <returns>An ordered list of migrations from V1 to V3.</returns>
    public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
    {
        var table = configuration.InBoxTableName;
        var schema = configuration.SchemaName;

        Identifiers.AssertSafe(table, nameof(IAmARelationalDatabaseConfiguration.InBoxTableName));
        if (schema is not null)
        {
            Identifiers.AssertSafe(schema, nameof(IAmARelationalDatabaseConfiguration.SchemaName));
        }

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
                UpScript: AddColumn(schema, table, "ContextKey", "VARCHAR(256)"),
                LogicalColumns: Cumulative(2),
                SourceReference: "787c31c52"),

            new BoxMigration(
                Version: 3,
                Description: "Add CausationId column",
                UpScript: AddColumn(schema, table, "CausationId", "VARCHAR(256)"),
                LogicalColumns: Cumulative(3),
                SourceReference: "#2541")
        ];
    }

    private static IReadOnlyCollection<string> Cumulative(int upToVersion)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (upToVersion >= 1) { set.UnionWith(s_v1Columns); }
        if (upToVersion >= 2) { set.UnionWith(s_v2AddedColumns); }
        if (upToVersion >= 3) { set.UnionWith(s_v3AddedColumns); }
        return set;
    }

    /// <summary>
    /// MySQL 5.7+ idempotent ADD COLUMN — runtime <c>information_schema.columns</c> probe drives
    /// a prepared-statement that conditionally emits the ALTER. When <paramref name="schema"/>
    /// is null, the probe targets <c>DATABASE()</c> and the ALTER is unqualified — original
    /// behaviour preserved. When non-null, both probe and ALTER are pinned to the configured
    /// schema (per PR #4039 reviewer item M4-1 / F1c). The added column is <c>NULL</c>-able —
    /// required because MySQL ADD COLUMN against a non-empty table must permit NULL or supply
    /// a DEFAULT, and we make no assumption about emptiness during bootstrap.
    /// </summary>
    private static string AddColumn(string? schema, string table, string column, string type)
    {
        var tableScopeForProbe = schema is null ? "DATABASE()" : $"'{schema}'";
        var qualifiedAlterTarget = schema is null ? $"`{table}`" : $"`{schema}`.`{table}`";
        return $@"SET @q = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = {tableScopeForProbe} AND table_name = '{table}' AND column_name = '{column}') = 0,
    'ALTER TABLE {qualifiedAlterTarget} ADD COLUMN `{column}` {type} NULL',
    'SELECT 1'));
PREPARE stmt FROM @q;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;";
    }
}
