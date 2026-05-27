using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.Spanner.Data;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.Spanner;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

// Spec 0029 FR1b/AC1b (ADR 0060 D3): Spanner does not derive from SqlBoxMigrationRunner (ADR 0057
// §6) and has no schema concept, so there is no scope knob on its runner at all — the only operator
// surface is BoxProvisioningOptions.MigrationHistoryScope. Selecting MigrationHistoryScope.PerSchema
// must therefore be a NO-OP: Spanner provisioning proceeds unaffected, history lands in the single
// BrighterMigrationHistory table, and the D3 "PerSchema + null SchemaName" guard MUST NOT fire.
// These characterization tests pin that no-op for both a non-null SchemaName (accepted and ignored
// by Spanner) and a null SchemaName.
[Collection("SpannerBoxProvisioning")]
public class SpannerPerSchemaNoOpTests : IAsyncLifetime
{
    private readonly string _connectionString = Const.ConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_per_schema_is_selected_with_a_non_null_schema_it_should_be_a_no_op_and_not_throw()
    {
        //Arrange — operator selects PerSchema and configures a non-null SchemaName. Spanner ignores both.
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: "billing");
        var provisioner = BuildProvisioner(config);

        //Act
        var exception = await Record.ExceptionAsync(() => provisioner.ProvisionAsync());

        //Assert — no throw and the table + single history table exist (no placement change).
        Assert.Null(exception);
        Assert.Equal(1, await TableCountAsync(_tableName));
        Assert.Equal(1, await HistoryRowCountAsync());
    }

    [Fact]
    public async Task When_per_schema_is_selected_with_a_null_schema_it_should_be_a_no_op_and_not_throw()
    {
        //Arrange — operator selects PerSchema with a null SchemaName. On a placement backend this
        // would trip the D3 guard; on Spanner it must not.
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: null);
        var provisioner = BuildProvisioner(config);

        //Act
        var exception = await Record.ExceptionAsync(() => provisioner.ProvisionAsync());

        //Assert — no ConfigurationException (D3 guard is gated off for Spanner).
        Assert.Null(exception);
        Assert.Equal(1, await TableCountAsync(_tableName));
        Assert.Equal(1, await HistoryRowCountAsync());
    }

    // Wire the provisioner through the DI registration with the operator-facing PerSchema option set —
    // the only place "PerSchema" can be expressed for Spanner. The single registered provisioner is
    // resolved and returned.
    private IAmABoxProvisioner BuildProvisioner(RelationalDatabaseConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var stubBuilder = new StubBrighterBuilder(services);
        stubBuilder.UseBoxProvisioning(opts =>
        {
            // Evident data: PerSchema is the scope under test.
            opts.MigrationHistoryScope = MigrationHistoryScope.PerSchema;
            opts.AddSpannerOutbox(config);
        });

        var provider = services.BuildServiceProvider();
        return provider.GetServices<IAmABoxProvisioner>().Single();
    }

    private async Task<long> TableCountAsync(string tableName)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();
        using var command = connection.CreateSelectCommand(
            "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName",
            new SpannerParameterCollection { { "TableName", SpannerDbType.String, tableName } });
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<long> HistoryRowCountAsync()
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();
        using var command = connection.CreateSelectCommand(
            "SELECT COUNT(1) FROM `BrighterMigrationHistory` WHERE `BoxTableName` = @BoxTableName",
            new SpannerParameterCollection { { "BoxTableName", SpannerDbType.String, _tableName } });
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            var dropTable = connection.CreateDdlCommand($"DROP TABLE IF EXISTS `{_tableName}`");
            await dropTable.ExecuteNonQueryAsync();
        }
        catch { }
    }

    private SpannerConnection CreateConnection()
    {
        var builder = new SpannerConnectionStringBuilder(_connectionString)
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        };
        return new SpannerConnection(builder);
    }
}
