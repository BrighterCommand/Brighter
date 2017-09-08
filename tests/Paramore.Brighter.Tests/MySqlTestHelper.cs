using System;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Paramore.Brighter.CommandStore.MySql;
using Paramore.Brighter.MessageStore.MySql;

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
            CreateMessageStoreTable();
        }

        public void SetupCommandDb()
        {
            CreateDatabase();
            CreateCommandStoreTable();
        }

        public MySqlCommandStoreConfiguration CommandStoreConfiguration => new MySqlCommandStoreConfiguration(_mysqlSettings.TestsBrighterConnectionString, _tableName);

        public MySqlMessageStoreConfiguration MessageStoreConfiguration => new MySqlMessageStoreConfiguration(_mysqlSettings.TestsBrighterConnectionString, _tableName);

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

        public void CreateMessageStoreTable()
        {
            using (var connection = new MySqlConnection(_mysqlSettings.TestsBrighterConnectionString))
            {
                _tableName = $"`message_{_tableName}`";
                var createTableSql = MySqlMessageStoreBuilder.GetDDL(_tableName);

                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = createTableSql;
                    command.ExecuteNonQuery();
                }
            }
        }

        public void CreateCommandStoreTable()
        {
            using (var connection = new MySqlConnection(_mysqlSettings.TestsBrighterConnectionString))
            {
                _tableName = $"`command_{_tableName}`";
                var createTableSql = MySqlCommandStoreBuilder.GetDDL(_tableName);

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