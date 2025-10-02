using System;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Outbox.MySql;

namespace Paramore.Brighter.MySQL.Tests
{
    public class MySqlTestHelper
    {
        private readonly bool _binaryMessagePayload;
        private string _tableName;
        private MySqlSettings _mysqlSettings;

        private IAmARelationalDbConnectionProvider _connectionProvider;
        public IAmARelationalDbConnectionProvider ConnectionProvider => _connectionProvider;

        public RelationalDatabaseConfiguration InboxConfiguration =>
            new(_mysqlSettings.TestsBrighterConnectionString, inboxTableName: _tableName,
                    binaryMessagePayload: _binaryMessagePayload);

        public RelationalDatabaseConfiguration OutboxConfiguration =>
            new(_mysqlSettings.TestsBrighterConnectionString, outBoxTableName: _tableName,
                binaryMessagePayload: _binaryMessagePayload);

        public MySqlTestHelper(bool binaryMessagePayload = false)
        {
            _binaryMessagePayload = binaryMessagePayload;
            var builder = new ConfigurationBuilder().AddEnvironmentVariables();
            var configuration = builder.Build();

            _mysqlSettings = new MySqlSettings();
            configuration.GetSection("MySql").Bind(_mysqlSettings);

            _tableName = $"test_{Guid.NewGuid()}";

            _connectionProvider =
                new MySqlConnectionProvider(
                    new RelationalDatabaseConfiguration(_mysqlSettings.TestsBrighterConnectionString));
        }

        public void CreateDatabase()
        {
            using var connection = new MySqlConnection(_mysqlSettings.TestsMasterConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"CREATE DATABASE IF NOT EXISTS BrighterTests;";
            command.ExecuteNonQuery();
        }

        public void SetupMessageDb()
        {
            CreateDatabase();
            CreateOutboxTable();
        }

        public void SetupCommandDb()
        {
            CreateDatabase();
            CreateInboxTable();
        }

        public void CleanUpDb()
        {
            using var connection = new MySqlConnection(_mysqlSettings.TestsBrighterConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $@"DROP TABLE IF EXISTS {_tableName}";
            command.ExecuteNonQuery();
        }

        public void CreateOutboxTable()
        {
            using var connection = new MySqlConnection(_mysqlSettings.TestsBrighterConnectionString);
            _tableName = $"`message_{_tableName}`";
            var createTableSql =
                MySqlOutboxBuilder.GetDDL(_tableName, hasBinaryMessagePayload: _binaryMessagePayload);

            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = createTableSql;
            command.ExecuteNonQuery();
        }

        public void CreateInboxTable()
        {
            using var connection = new MySqlConnection(_mysqlSettings.TestsBrighterConnectionString);
            _tableName = $"`command_{_tableName}`";
            var createTableSql = MySqlInboxBuilder.GetDDL(_tableName, _binaryMessagePayload);

            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = createTableSql;
            command.ExecuteNonQuery();
        }
    }

    internal sealed class MySqlSettings
    {
        public string TestsBrighterConnectionString { get; set; } =
            "Server=localhost;Uid=root;Pwd=root;Database=BrighterTests";

        public string TestsMasterConnectionString { get; set; } = "Server=localhost;Uid=root;Pwd=root;";
    }
}
