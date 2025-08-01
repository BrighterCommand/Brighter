using System;
using Google.Cloud.Spanner.Data;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.Inbox.Spanner;
using Paramore.Brighter.Outbox.Spanner;

namespace Paramore.Brighter.Gcp.Tests.Spanner;

internal sealed class SpannerTestHelper
{
    private readonly bool _binaryMessagePayload;
    private readonly SpannerSqlSettings _spannerSqlSettings;
    private string _tableName;

    public RelationalDatabaseConfiguration Configuration 
        => new(_spannerSqlSettings.TestsBrighterConnectionString, outBoxTableName: _tableName, binaryMessagePayload: _binaryMessagePayload);
        
    public RelationalDatabaseConfiguration InboxConfiguration 
        => new(_spannerSqlSettings.TestsBrighterConnectionString, inboxTableName: _tableName);

    public SpannerTestHelper(bool binaryMessagePayload = false)
    {
        _binaryMessagePayload = binaryMessagePayload;
        var builder = new ConfigurationBuilder().AddEnvironmentVariables();
        var configuration = builder.Build();

        _spannerSqlSettings = new SpannerSqlSettings();
        configuration.GetSection("Spanner").Bind(_spannerSqlSettings);

        _tableName = $"test_{Guid.NewGuid():N}";
    }


    public void SetupMessageDb()
    {
        CreateOutboxTable();
    }
        
    public void SetupCommandDb()
    {
        CreateInboxTable();
    }

    private void CreateOutboxTable()
    {
        using var connection = new SpannerConnection(_spannerSqlSettings.TestsBrighterConnectionString);
        _tableName = $"message_{_tableName}";
        var createTableSql = SpannerOutboxBuilder.GetDDL(_tableName, Configuration.BinaryMessagePayload);

        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        command.ExecuteNonQuery();
    }

    private void CreateInboxTable()
    {
        using var connection = new SpannerConnection(_spannerSqlSettings.TestsBrighterConnectionString);
        _tableName = $"command_{_tableName}";
        var createTableSql = SpannerInboxBuilder.GetDDL(_tableName);

        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        command.ExecuteNonQuery();
    }

    public void CleanUpDb()
    {
        using var connection = new SpannerConnection(_spannerSqlSettings.TestsBrighterConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {_tableName}";
        command.ExecuteNonQuery();
    }
}


internal sealed class SpannerSqlSettings
{
    public string TestsBrighterConnectionString { get; set; }
        = "Host=localhost;Username=postgres;Password=password;Database=brightertests;Include Error Detail=true;";
}
