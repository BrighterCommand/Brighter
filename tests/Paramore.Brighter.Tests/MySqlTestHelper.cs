using System;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Paramore.Brighter.CommandStore.MySql;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.MessageStore.MySql;
using Paramore.Brighter.Outbox.MySql;

namespace Paramore.Brighter.Tests
{
    public class MySqlTestHelper
    {
        private string _tableName;
        private MySqlSettings _mysqlSettings;

        public MySqlTestHelper()
        {
            var builder = new ConfigurationBuilder().AddEnvironmentVariables();
            var configuration = builder.Build();

            _mysqlSettings = new MySqlSettings();
            configuration.GetSection("MySql").Bind(_mysqlSettings);

            _tableName = $"test_{Guid.NewGuid()}";
        }

       public void CreateDatabase()
        {
            using (var connection = new MySqlConnection(_mysqlSettings.TestsMasterConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"CREATE DATABASE IF NOT EXISTS BrighterTests;";
                    command.ExecuteNonQuery();
                }
            }
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

        public MySqlInboxConfiguration InboxConfiguration => new MySqlInboxConfiguration(_mysqlSettings.TestsBrighterConnectionString, _tableName);

        public MySqlOutboxConfiguration OutboxConfiguration => new MySqlOutboxConfiguration(_mysqlSettings.TestsBrighterConnectionString, _tableName);

        public void CleanUpDb()
        {
            using (var connection = new MySqlConnection(_mysqlSettings.TestsBrighterConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"DROP TABLE IF EXISTS {_tableName}";
                    command.ExecuteNonQuery();
                }
            }
        }

        public void CreateOutboxTable()
        {
            using (var connection = new MySqlConnection(_mysqlSettings.TestsBrighterConnectionString))
            {
                _tableName = $"`message_{_tableName}`";
                var createTableSql = MySqlOutboxBuilder.GetDDL(_tableName);

                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = createTableSql;
                    command.ExecuteNonQuery();
                }
            }
        }

        public void CreateInboxTable()
        {
            using (var connection = new MySqlConnection(_mysqlSettings.TestsBrighterConnectionString))
            {
                _tableName = $"`command_{_tableName}`";
                var createTableSql = MySqlInboxBuilder.GetDDL(_tableName);

                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = createTableSql;
                    command.ExecuteNonQuery();
                }
            }
        }
    }

    internal class MySqlSettings
    {
        public string TestsBrighterConnectionString { get; set; } = "Server=localhost;Uid=root;Pwd=root;Database=BrighterTests";
        public string TestsMasterConnectionString { get; set; } = "Server=localhost;Uid=root;Pwd=root;";
    }
}