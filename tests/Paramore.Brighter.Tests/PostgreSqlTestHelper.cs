using System;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.Outbox.PostgreSql;

namespace Paramore.Brighter.Tests
{
    internal class PostgreSqlTestHelper
    {
        private readonly PostgreSqlSettings _postgreSqlSettings;
        private string _tableName;

        public PostgreSqlTestHelper()
        {
            var builder = new ConfigurationBuilder().AddEnvironmentVariables();
            var configuration = builder.Build();

            _postgreSqlSettings = new PostgreSqlSettings();
            configuration.GetSection("PostgreSql").Bind(_postgreSqlSettings);

            _tableName = $"test_{Guid.NewGuid():N}";
        }

        public PostgreSqlOutboxConfiguration OutboxConfiguration => new PostgreSqlOutboxConfiguration(_postgreSqlSettings.TestsBrighterConnectionString, _tableName);

        public void SetupMessageDb()
        {
            CreateDatabase();
            CreateOutboxTable();
        }

        private void CreateDatabase()
        {
            var createDatabase = false;
            using (var connection = new NpgsqlConnection(_postgreSqlSettings.TestsMasterConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT datname FROM pg_database WHERE datname = 'brightertests';";
                    var rowsEffected = command.ExecuteReader();

                    if (!rowsEffected.HasRows)
                        createDatabase = true;
                }
            }

            if (createDatabase)
                using (var connection = new NpgsqlConnection(_postgreSqlSettings.TestsMasterConnectionString))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"CREATE DATABASE brightertests";
                        command.ExecuteNonQuery();
                    }
                }
        }

        public void CleanUpTable()
        {
            using (var connection = new NpgsqlConnection(_postgreSqlSettings.TestsBrighterConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"DROP TABLE IF EXISTS {_tableName}";
                    command.ExecuteNonQuery();
                }
            }
        }

        private void CreateOutboxTable()
        {
            using (var connection = new NpgsqlConnection(_postgreSqlSettings.TestsBrighterConnectionString))
            {
                _tableName = $"message_{_tableName}";
                var createTableSql = PostgreSqlOutboxBulder.GetDDL(_tableName);

                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = createTableSql;
                    command.ExecuteNonQuery();
                }
            }
        }
    }


    internal class PostgreSqlSettings
    {
        public string TestsBrighterConnectionString { get; set; } = "Host=localhost;Username=postgres;Password=password;Database=brightertests;";

        public string TestsMasterConnectionString { get; set; } = "Host=localhost;Username=postgres;Password=password;";
    }
}
