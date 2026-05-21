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
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.BoxProvisioning.Spanner;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Outbox.Spanner;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

[Collection("SpannerBoxProvisioning")]
public class When_spanner_provisioner_runs_against_existing_outbox_with_mismatched_payload_mode_it_should_throw_configuration_exception : IAsyncLifetime
{
    private readonly string _connectionString = Const.ConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task Should_throw_when_existing_outbox_body_is_string_and_provisioner_is_configured_for_binary()
    {
        //Arrange — text-mode outbox already exists; configure provisioner for binary.
        await CreateTable(SpannerOutboxBuilder.GetDDL(_tableName, binaryMessagePayload: false));

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            binaryMessagePayload: true);
        var provisioner = new SpannerOutboxProvisioner(
            new SpannerBoxDetectionHelper(),
            new SpannerPayloadModeValidator(),
            config,
            new SpannerBoxMigrationRunner(config));

        //Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());
        Assert.Contains("mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_throw_when_existing_outbox_body_is_bytes_and_provisioner_is_configured_for_text()
    {
        //Arrange — binary (BYTES) outbox already exists; configure provisioner for text.
        await CreateTable(SpannerOutboxBuilder.GetDDL(_tableName, binaryMessagePayload: true));

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            binaryMessagePayload: false);
        var provisioner = new SpannerOutboxProvisioner(
            new SpannerBoxDetectionHelper(),
            new SpannerPayloadModeValidator(),
            config,
            new SpannerBoxMigrationRunner(config));

        //Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync());
        Assert.Contains("mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private async Task CreateTable(string ddl)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();
        var ddlCommand = connection.CreateDdlCommand(ddl);
        await ddlCommand.ExecuteNonQueryAsync();
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
