using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.PostgresSQL.Tests
{
    internal sealed class PostgresSqlTestHelper
    {
        private readonly bool _binaryMessagePayload;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PostgresSqlTestHelper>();
        private readonly PostgreSqlSettings _postgreSqlSettings;
        private readonly string _tableName;
        private readonly object _syncObject = new();

        public RelationalDatabaseConfiguration Configuration => new(PostgreSqlSettings.TestsBrighterConnectionString, queueStoreTable: _tableName, binaryMessagePayload: _binaryMessagePayload);

        public PostgresSqlTestHelper(bool binaryMessagePayload = false)
        {
            _binaryMessagePayload = binaryMessagePayload;
            var builder = new ConfigurationBuilder().AddEnvironmentVariables();
            var configuration = builder.Build();

            _postgreSqlSettings = new PostgreSqlSettings();
            configuration.GetSection("PostgreSql").Bind(_postgreSqlSettings);

            _tableName = $"test_{Guid.NewGuid():N}";
        }


        public void SetupDatabase()
        {
            CreateDatabase();
        }
        
        private void CreateDatabase()
        {
            lock (_syncObject)
            {
                var createDatabase = false;
                using (var connection = new NpgsqlConnection(PostgreSqlSettings.TestsMasterConnectionString))
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
                        using var connection = new NpgsqlConnection(PostgreSqlSettings.TestsMasterConnectionString);
                        connection.Open();
                        using var command = connection.CreateCommand();
                        command.CommandText = @"CREATE DATABASE brightertests";
                        command.ExecuteNonQuery();
                    }
                    catch (PostgresException sqlException)
                    {
                        if (sqlException.SqlState == PostgresErrorCodes.UniqueViolation)
                        {
                            s_logger.LogWarning("PostgresSQL: We tried our best with errors around already created database, but failed");
                        }

                    }
            }
        }
    }

    internal sealed class PostgreSqlSettings
    {
        public static string TestsBrighterConnectionString  => "Host=localhost;Username=postgres;Password=password;Database=brightertests;Include Error Detail=true;";

        public static string TestsMasterConnectionString => "Host=localhost;Username=postgres;Password=password;Include Error Detail=true;";
    }
}
