﻿using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Paramore.Brighter.Inbox.Postgres;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Outbox.PostgreSql;

namespace Paramore.Brighter.PostgresSQL.Tests
{
    internal class PostgresSqlTestHelper
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PostgresSqlTestHelper>();
        private readonly PostgreSqlSettings _postgreSqlSettings;
        private string _tableName;
        private readonly object syncObject = new object();

        public PostgresSqlTestHelper()
        {
            var builder = new ConfigurationBuilder().AddEnvironmentVariables();
            var configuration = builder.Build();

            _postgreSqlSettings = new PostgreSqlSettings();
            configuration.GetSection("PostgreSql").Bind(_postgreSqlSettings);

            _tableName = $"test_{Guid.NewGuid():N}";
        }

        public PostgreSqlOutboxConfiguration OutboxConfiguration => new PostgreSqlOutboxConfiguration(_postgreSqlSettings.TestsBrighterConnectionString, _tableName);
        
        public PostgresSqlInboxConfiguration InboxConfiguration => new PostgresSqlInboxConfiguration(_postgreSqlSettings.TestsBrighterConnectionString, _tableName);


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


        private void CreateDatabase()
        {
            lock (syncObject)
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
                    try
                    {
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
                    catch (PostgresException sqlException)
                    {
                        if (sqlException.SqlState == PostgresErrorCodes.UniqueViolation)
                        {
                            s_logger.LogWarning("PostgresSQL: We tried our best with errors around already created database, but failed");
                            return;
                        }

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
        public void CreateInboxTable()
        {
            using (var connection = new NpgsqlConnection(_postgreSqlSettings.TestsBrighterConnectionString))
            {
                _tableName = $"command_{_tableName}";
                var createTableSql = PostgresSqlInboxBuilder.GetDDL(_tableName);

                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = createTableSql;
                    command.ExecuteNonQuery();
                }
            }
        }

        public void CleanUpDb()
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
    }


    internal class PostgreSqlSettings
    {
        public string TestsBrighterConnectionString { get; set; } = "Host=localhost;Username=postgres;Password=password;Database=brightertests;";

        public string TestsMasterConnectionString { get; set; } = "Host=localhost;Username=postgres;Password=password;";
    }
}
