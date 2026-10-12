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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Runs box migrations against a PostgreSQL database. Derives from
/// <see cref="SqlBoxMigrationRunner{TConnection,TTransaction}"/> for the
/// success/failure orchestration and supplies the per-backend hooks. Uses an injected
/// <see cref="IPostgreSqlAdvisoryLock"/> (default <see cref="PostgreSqlAdvisoryLock"/>) for
/// concurrency control via <see cref="PostgreSqlProvisioningUnitOfWork"/>; the dispatch into
/// fresh / bootstrap / normal paths happens in the base after re-detection under the UoW.
/// </summary>
public class PostgreSqlBoxMigrationRunner : SqlBoxMigrationRunner<NpgsqlConnection, NpgsqlTransaction>
{
    private const string MIGRATION_HISTORY_TABLE = "__BrighterMigrationHistory";
    // The Global history schema. Qualified explicitly, so history cannot land in whichever schema
    // is first on the connection's search_path.
    private const string HISTORY_TABLE_SCHEMA = "public";

    private readonly IPostgreSqlAdvisoryLock _advisoryLock;

    /// <summary>
    /// Initialises the runner with an explicit detection helper and optional UoW dependencies.
    /// </summary>
    public PostgreSqlBoxMigrationRunner(
        PostgreSqlBoxDetectionHelper detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        IPostgreSqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        TimeSpan? lockTimeout = null,
        IAmABrighterTracer? tracer = null,
        MigrationHistoryScope scope = MigrationHistoryScope.Global)
        : base(detectionHelper, catalog, configuration, lockTimeout ?? TimeSpan.FromSeconds(30),
            logger ?? ApplicationLogging.CreateLogger<PostgreSqlBoxMigrationRunner>(),
            tracer, scope)
    {
        _advisoryLock = advisoryLock ?? new PostgreSqlAdvisoryLock();
    }

    /// <summary>
    /// Initialises the runner with a default <see cref="PostgreSqlBoxDetectionHelper"/>.
    /// </summary>
    public PostgreSqlBoxMigrationRunner(
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        IPostgreSqlAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        IAmABrighterTracer? tracer = null,
        MigrationHistoryScope scope = MigrationHistoryScope.Global)
        : this(new PostgreSqlBoxDetectionHelper(), catalog, configuration, advisoryLock, logger, lockTimeout, tracer, scope)
    {
    }

    /// <inheritdoc />
    protected override DbSystem DbSystem => DbSystem.Postgresql;

    /// <inheritdoc />
    protected override string? DefaultHistorySchema => HISTORY_TABLE_SCHEMA;

    /// <inheritdoc />
    protected override bool SupportsPerSchemaHistory => true;

    protected override async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(Configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    protected override Task<IAmAProvisioningUnitOfWork<NpgsqlTransaction>> CreateUnitOfWorkAsync(
        NpgsqlConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
        => Task.FromResult<IAmAProvisioningUnitOfWork<NpgsqlTransaction>>(
            new PostgreSqlProvisioningUnitOfWork(connection, _advisoryLock, Logger));

    /// <inheritdoc />
    /// <remarks>
    /// Includes the schema, so same-named tables in different schemas (<c>public.Outbox</c>,
    /// <c>billing.Outbox</c>) take different locks; and folds both names to lowercase, so
    /// differently-cased configurations of one physical table share a lock.
    /// </remarks>
    protected override string LockResourceFor(string? schemaName, string tableName)
        => $"BrighterMigration_{PgIdentifier.Normalize(schemaName ?? HISTORY_TABLE_SCHEMA)}.{PgIdentifier.Normalize(tableName)}";


    // Never null here: DefaultHistorySchema is "public", and the base rejects PerSchema without a
    // SchemaName before migrating.
    private string ValidatedHistorySchema()
    {
        var resolvedHistorySchema = ResolveHistorySchema()!;
        Identifiers.AssertSafe(resolvedHistorySchema, nameof(resolvedHistorySchema));
        return resolvedHistorySchema;
    }

    // Quoted, and so folded, the same way the detection helper reads it.
    private string QuotedHistorySchema() => PgIdentifier.Quote(ValidatedHistorySchema());

    protected override async Task EnsureHistoryTableAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName, string tableName,
        CancellationToken cancellationToken)
    {
        var resolvedHistorySchema = ValidatedHistorySchema();
        var historySchemaQuoted = PgIdentifier.Quote(resolvedHistorySchema);
        var historySchemaFolded = PgIdentifier.Normalize(resolvedHistorySchema);

        var historyIsSeparateFromLegacy = !string.Equals(historySchemaFolded, HISTORY_TABLE_SCHEMA, StringComparison.Ordinal);

        await CreateHistoryTableAsync(connection, transaction, historySchemaQuoted, cancellationToken);

        if (historyIsSeparateFromLegacy)
        {
            await SeedHistoryFromLegacyAsync(
                connection, transaction, historySchemaQuoted, resolvedHistorySchema,
                schemaName ?? resolvedHistorySchema, tableName, cancellationToken);
        }
    }

    // A provisioner for another box can race this CREATE (ADR 0057 §3). Postgres aborts the whole
    // transaction on the loser's duplicate error even when it is caught, so the CREATE runs inside
    // a savepoint that the catch rolls back to.
    private async Task CreateHistoryTableAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string historySchemaQuoted,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
CREATE TABLE IF NOT EXISTS {historySchemaQuoted}.""{MIGRATION_HISTORY_TABLE}"" (
    ""MigrationVersion"" INT NOT NULL,
    ""SchemaName"" VARCHAR(256) NOT NULL DEFAULT 'public',
    ""BoxTableName"" VARCHAR(256) NOT NULL,
    ""Description"" VARCHAR(512) NOT NULL,
    ""AppliedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (""SchemaName"", ""BoxTableName"", ""MigrationVersion"")
)";
        if (transaction is not null)
        {
            await transaction.SaveAsync(HistoryTableSavepoint, cancellationToken);
        }
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.ReleaseAsync(HistoryTableSavepoint, cancellationToken);
            }
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UniqueViolation
            || ex.SqlState == PostgresErrorCodes.DuplicateTable
            || ex.SqlState == PostgresErrorCodes.DuplicateObject)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(HistoryTableSavepoint, cancellationToken);
            }

            Logger.LogDebug(ex,
                "{HistoryTable} already created by racing session (Postgres SqlState {SqlState})",
                MIGRATION_HISTORY_TABLE, ex.SqlState);
            Activity.Current?.AddEvent(new ActivityEvent(
                BrighterSemanticConventions.BoxMigrationEventHistoryTableRaceSwallowed));
        }
    }

    // foldedSchema must already be folded by PgIdentifier.Normalize: information_schema holds the
    // case PostgreSQL stored, so an unfolded name would miss a table created from a mixed-case one.
    private async Task<bool> DoesHistoryTableExistAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string foldedSchema,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT EXISTS(SELECT 1 FROM information_schema.tables " +
            "WHERE table_name = @HistoryTableName AND table_schema = @SchemaName)";
        command.Parameters.AddWithValue("@HistoryTableName", MIGRATION_HISTORY_TABLE);
        command.Parameters.AddWithValue("@SchemaName", foldedSchema);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool exists && exists;
    }

    // Copies this box's rows from the legacy public history table into the per-schema one
    // (ADR 0060 D5). Runs on every PerSchema provision, not only the first: each box type that
    // flips must seed its own rows, and the NOT EXISTS guard makes a re-run copy nothing. All five
    // columns are copied, so a seeded row is indistinguishable from the original. No legacy table
    // means no Global predecessor, so nothing to copy.
    private async Task SeedHistoryFromLegacyAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction,
        string perSchemaQuoted, string perSchemaBare, string boxSchema, string boxTableName,
        CancellationToken cancellationToken)
    {
        const string legacySchema = HISTORY_TABLE_SCHEMA;
        var legacyFolded = PgIdentifier.Normalize(legacySchema);
        var legacyQuoted = PgIdentifier.Quote(legacySchema);
        bool legacyExists;
        try
        {
            legacyExists = await DoesHistoryTableExistAsync(connection, transaction, legacyFolded, cancellationToken);
        }
        catch (PostgresException ex) when (IsLegacyHistoryReadPermissionDenied(ex))
        {
            throw BuildLegacyHistoryReadDeniedException(legacyQuoted, perSchemaQuoted, ex);
        }
        if (!legacyExists)
        {
            return;
        }

        Identifiers.AssertSafe(boxSchema, nameof(boxSchema));

        int rowsCopied;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $@"
INSERT INTO {perSchemaQuoted}.""{MIGRATION_HISTORY_TABLE}""
    (""MigrationVersion"", ""SchemaName"", ""BoxTableName"", ""Description"", ""AppliedAt"")
SELECT src.""MigrationVersion"", src.""SchemaName"", src.""BoxTableName"", src.""Description"", src.""AppliedAt""
FROM {legacyQuoted}.""{MIGRATION_HISTORY_TABLE}"" AS src
WHERE src.""SchemaName"" = @SchemaName
  AND src.""BoxTableName"" = @BoxTableName
  AND NOT EXISTS (
      SELECT 1 FROM {perSchemaQuoted}.""{MIGRATION_HISTORY_TABLE}"" AS tgt
      WHERE tgt.""SchemaName"" = src.""SchemaName""
        AND tgt.""BoxTableName"" = src.""BoxTableName""
        AND tgt.""MigrationVersion"" = src.""MigrationVersion"")";
            command.Parameters.AddWithValue("@SchemaName", PgIdentifier.Normalize(boxSchema));
            command.Parameters.AddWithValue("@BoxTableName", PgIdentifier.Normalize(boxTableName));
            try
            {
                rowsCopied = await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException ex) when (IsLegacyHistoryReadPermissionDenied(ex))
            {
                throw BuildLegacyHistoryReadDeniedException(legacyQuoted, perSchemaQuoted, ex);
            }
        }

        if (rowsCopied > 0)
        {
            Logger.LogInformation(
                "Seeded {RowCount} legacy history row(s) for {BoxTable} from {LegacySchema} to {TargetSchema}",
                rowsCopied, boxTableName, legacySchema, perSchemaBare);
            Activity.Current?.AddEvent(new ActivityEvent(
                BrighterSemanticConventions.BoxMigrationEventLegacyHistorySeeded,
                default,
                new ActivityTagsCollection
                {
                    { BrighterSemanticConventions.BoxMigrationSeedRowCount, rowsCopied }
                }));
        }
    }

    // Only a missing privilege means "grant SELECT"; any other failure propagates unchanged.
    private static bool IsLegacyHistoryReadPermissionDenied(PostgresException ex) =>
        ex.SqlState == PostgresErrorCodes.InsufficientPrivilege;

    private static ConfigurationException BuildLegacyHistoryReadDeniedException(
        string legacyQuoted, string perSchemaQuoted, Exception inner) =>
        new ConfigurationException(
            $"Brighter PerSchema migration: every provision run reads the legacy default-schema " +
            $"history table {legacyQuoted}.\"{MIGRATION_HISTORY_TABLE}\" so any unseeded tenant " +
            $"rows can be copied into the per-schema history (the per-row NOT EXISTS guard makes " +
            $"steady-state runs a zero-row no-op, but the SELECT against the legacy table runs " +
            $"every time). Grant the runner SELECT on that table (and INSERT on " +
            $"{perSchemaQuoted}.\"{MIGRATION_HISTORY_TABLE}\") for the lifetime of the PerSchema " +
            $"deployment and retry. The original provider exception is preserved as the inner " +
            $"exception.",
            inner);

    private const string HistoryTableSavepoint = "ensure_history_table";

    protected override async Task RunFreshPathAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName, string tableName,
        string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
    {
        var effectiveSchema = schemaName ?? HISTORY_TABLE_SCHEMA;

        await ExecuteDdlAsync(connection, transaction!, freshInstallDdl, cancellationToken);

        await InsertHistoryRowAsync(
            connection, transaction!, effectiveSchema, tableName,
            latestVersion, $"fresh install at V{latestVersion}", cancellationToken);
    }

    protected override async Task RunBootstrapPathAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        var effectiveSchema = schemaName ?? HISTORY_TABLE_SCHEMA;

        var detected = await DetectionHelper.DetectCurrentVersionAsync(
            connection, tableName, effectiveSchema, boxType, migrations, cancellationToken, transaction);

        if (detected == -1)
        {
            var discriminator = DetectionHelper.DiscriminatorFor(boxType);
            throw new ConfigurationException(
                $"Table '{effectiveSchema}.{tableName}' is not a Brighter {boxType.ToString().ToLowerInvariant()}: " +
                $"missing discriminator column '{discriminator}'.");
        }

        if (detected == 0)
        {
            throw new ConfigurationException(
                $"Table '{effectiveSchema}.{tableName}' does not match any known schema version. " +
                $"Cannot bootstrap a Brighter {boxType.ToString().ToLowerInvariant()} from an unrecognised column set.");
        }

        await InsertHistoryRowAsync(
            connection, transaction!, effectiveSchema, tableName,
            detected, $"bootstrap: detected at V{detected}", cancellationToken);

        for (var i = 0; i < migrations.Count; i++)
        {
            var migration = migrations[i];
            if (migration.Version <= detected) continue;

            await ExecuteUpScriptAsync(connection, transaction!, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, transaction!, effectiveSchema, tableName,
                migration.Version, migration.Description.Value, cancellationToken);
        }
    }

    protected override async Task RunNormalPathAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        var effectiveSchema = schemaName ?? HISTORY_TABLE_SCHEMA;

        var maxVersion = await DetectionHelper.GetMaxVersionAsync(
            connection, tableName, effectiveSchema, ResolveHistorySchema(), cancellationToken, transaction);

        foreach (var migration in migrations)
        {
            if (migration.Version <= maxVersion) continue;

            await ExecuteUpScriptAsync(connection, transaction!, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, transaction!, effectiveSchema, tableName,
                migration.Version, migration.Description.Value, cancellationToken);
        }
    }

    private static Task ExecuteUpScriptAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        IAmABoxMigration migration, CancellationToken cancellationToken)
        => ExecuteDdlAsync(connection, transaction, migration.UpScript.Value, cancellationToken);

    private static async Task ExecuteDdlAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string ddl, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ddl;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertHistoryRowAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schemaName, string tableName,
        int version, string description, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
INSERT INTO {QuotedHistorySchema()}.""{MIGRATION_HISTORY_TABLE}"" (""MigrationVersion"", ""SchemaName"", ""BoxTableName"", ""Description"")
VALUES (@Version, @SchemaName, @BoxTableName, @Description)";
        command.Parameters.AddWithValue("@Version", version);
        command.Parameters.AddWithValue("@SchemaName", PgIdentifier.Normalize(schemaName));
        command.Parameters.AddWithValue("@BoxTableName", PgIdentifier.Normalize(tableName));
        command.Parameters.AddWithValue("@Description", description);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
