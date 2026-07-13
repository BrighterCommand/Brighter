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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.BoxProvisioning.Oracle;

/// <summary>
/// Runs box migrations against Oracle. Derives from
/// <see cref="SqlBoxMigrationRunner{TConnection,TTransaction}"/> for orchestration and
/// supplies Oracle-specific hooks for connection, advisory lock UoW, history table, and
/// migration-application paths.
/// </summary>
/// <remarks>
/// Oracle DDL auto-commits per statement. The runner therefore coordinates concurrency with
/// <see cref="IOracleAdvisoryLock"/> and does not rely on a transaction rollback model for DDL.
/// </remarks>
public class OracleBoxMigrationRunner : SqlBoxMigrationRunner<OracleConnection, OracleTransaction>
{
    private const string MIGRATION_HISTORY_TABLE = "__BRIGHTERMIGRATIONHISTORY";
    private readonly IOracleAdvisoryLock _advisoryLock;

    /// <summary>
    /// Initialises the runner with an explicit detection helper and optional dependencies.
    /// </summary>
    public OracleBoxMigrationRunner(
        OracleBoxDetectionHelper detectionHelper,
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        IOracleAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        TimeSpan? lockTimeout = null,
        IAmABrighterTracer? tracer = null,
        MigrationHistoryScope scope = MigrationHistoryScope.Global)
        : base(detectionHelper, catalog, configuration, lockTimeout ?? TimeSpan.FromSeconds(30),
            logger ?? ApplicationLogging.CreateLogger<OracleBoxMigrationRunner>(),
            tracer, scope)
    {
        _advisoryLock = advisoryLock ?? new OracleAdvisoryLock();
    }

    /// <summary>
    /// Convenience ctor used by registration extensions.
    /// </summary>
    public OracleBoxMigrationRunner(
        IAmABoxMigrationCatalog catalog,
        IAmARelationalDatabaseConfiguration configuration,
        TimeSpan lockTimeout,
        IOracleAdvisoryLock? advisoryLock = null,
        ILogger? logger = null,
        IAmABrighterTracer? tracer = null,
        MigrationHistoryScope scope = MigrationHistoryScope.Global)
        : this(new OracleBoxDetectionHelper(), catalog, configuration, advisoryLock, logger, lockTimeout, tracer, scope)
    {
    }

    /// <inheritdoc />
    protected override DbSystem DbSystem => DbSystem.Oracle;

    /// <inheritdoc />
    /// <remarks>
    /// Oracle history is stored in the connected user's schema; there is no fixed default
    /// schema constant for this backend.
    /// </remarks>
    protected override string? DefaultHistorySchema => null;

    protected override async Task<OracleConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new OracleConnection(Configuration.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    protected override Task<IAmAProvisioningUnitOfWork<OracleTransaction>> CreateUnitOfWorkAsync(
        OracleConnection connection, string? schemaName, string tableName, CancellationToken cancellationToken)
    {
        _ = schemaName;
        _ = cancellationToken;
        return Task.FromResult<IAmAProvisioningUnitOfWork<OracleTransaction>>(
            new OracleProvisioningUnitOfWork(connection, _advisoryLock, Logger, tableName));
    }

    protected override string LockResourceFor(string? schemaName, string tableName)
        => OracleMigrationLockName.For(schemaName, tableName);

    protected override async Task EnsureHistoryTableAsync(
        OracleConnection connection, OracleTransaction? transaction, string? schemaName, string tableName,
        CancellationToken cancellationToken)
    {
        _ = transaction;
        _ = schemaName;
        _ = tableName;

        const string ddl = @"
CREATE TABLE __BRIGHTERMIGRATIONHISTORY (
    MigrationVersion NUMBER(10) NOT NULL,
    SchemaName VARCHAR2(256) NOT NULL,
    BoxTableName VARCHAR2(256) NOT NULL,
    Description NVARCHAR2(512) NOT NULL,
    AppliedAt TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
    CONSTRAINT PK_BrighterMigrationHistory PRIMARY KEY (SchemaName, BoxTableName, MigrationVersion)
)";

        var escapedDdl = ddl.Replace("'", "''");

        using var command = (OracleCommand)connection.CreateCommand();
        command.CommandText = $@"
BEGIN
  EXECUTE IMMEDIATE '{escapedDdl}';
EXCEPTION
  WHEN OTHERS THEN
    IF SQLCODE != -955 THEN
      RAISE;
    END IF;
END;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    protected override async Task RunFreshPathAsync(
        OracleConnection connection, OracleTransaction? transaction, string? schemaName, string tableName,
        string freshInstallDdl, int latestVersion, CancellationToken cancellationToken)
    {
        _ = transaction;
        var effectiveSchema = await ResolveSchemaAsync(connection, schemaName, cancellationToken);

        await ExecuteDdlAsync(connection, freshInstallDdl, cancellationToken);

        await InsertHistoryRowAsync(
            connection, effectiveSchema, tableName,
            latestVersion, $"fresh install at V{latestVersion}", cancellationToken);
    }

    protected override async Task RunBootstrapPathAsync(
        OracleConnection connection, OracleTransaction? transaction, string? schemaName, string tableName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        var effectiveSchema = await ResolveSchemaAsync(connection, schemaName, cancellationToken);

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
            connection, effectiveSchema, tableName,
            detected, $"bootstrap: detected at V{detected}", cancellationToken);

        for (var i = 0; i < migrations.Count; i++)
        {
            var migration = migrations[i];
            if (migration.Version <= detected) continue;

            await ExecuteUpScriptAsync(connection, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, effectiveSchema, tableName,
                migration.Version, migration.Description.Value, cancellationToken);
        }
    }

    protected override async Task RunNormalPathAsync(
        OracleConnection connection, OracleTransaction? transaction, string? schemaName, string tableName,
        IReadOnlyList<IAmABoxMigration> migrations, CancellationToken cancellationToken)
    {
        var effectiveSchema = await ResolveSchemaAsync(connection, schemaName, cancellationToken);

        var maxVersion = await DetectionHelper.GetMaxVersionAsync(
            connection, tableName, effectiveSchema, ResolveHistorySchema(), cancellationToken, transaction);

        foreach (var migration in migrations)
        {
            if (migration.Version <= maxVersion) continue;

            await ExecuteUpScriptAsync(connection, migration, cancellationToken);
            await InsertHistoryRowAsync(
                connection, effectiveSchema, tableName,
                migration.Version, migration.Description.Value, cancellationToken);
        }
    }

    private static Task ExecuteUpScriptAsync(
        OracleConnection connection, IAmABoxMigration migration,
        CancellationToken cancellationToken)
        => ExecuteDdlAsync(connection, migration.UpScript.Value, cancellationToken);

    private static async Task ExecuteDdlAsync(
        OracleConnection connection, string ddl,
        CancellationToken cancellationToken)
    {
        using var command = (OracleCommand)connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = ddl;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertHistoryRowAsync(
        OracleConnection connection, string schemaName, string tableName,
        int version, string description, CancellationToken cancellationToken)
    {
        using var command = (OracleCommand)connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"
INSERT INTO __BRIGHTERMIGRATIONHISTORY (MigrationVersion, SchemaName, BoxTableName, Description)
VALUES (:Version, :SchemaName, :BoxTableName, :Description)";
        command.Parameters.Add(new OracleParameter("Version", version));
        command.Parameters.Add(new OracleParameter("SchemaName", schemaName));
        command.Parameters.Add(new OracleParameter("BoxTableName", tableName));
        command.Parameters.Add(new OracleParameter("Description", description));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> ResolveSchemaAsync(
        OracleConnection connection, string? schemaName,
        CancellationToken cancellationToken)
    {
        if (schemaName is not null)
        {
            return schemaName;
        }

        using var command = (OracleCommand)connection.CreateCommand();
        command.CommandText = "SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') FROM DUAL";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result?.ToString()
               ?? throw new InvalidOperationException(
                   "Could not resolve the current Oracle schema via SYS_CONTEXT('USERENV','CURRENT_SCHEMA').");
    }
}
