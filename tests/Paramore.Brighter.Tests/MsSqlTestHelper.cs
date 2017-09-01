using System;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.CommandStore.MsSql;
using Paramore.Brighter.MessageStore.MsSql;

namespace Paramore.Brighter.Tests
{
    public class MsSqlTestHelper
    {
        private string _tableName;
        private SqlSettings _sqlSettings;

        public MsSqlTestHelper()
        {
            var builder = new ConfigurationBuilder().AddEnvironmentVariables();
            var configuration = builder.Build();

            _sqlSettings = new SqlSettings();
            configuration.GetSection("Sql").Bind(_sqlSettings);

            _tableName = $"test_{Guid.NewGuid()}";

        }

       public void CreateDatabase()
        {
            using (var connection = new SqlConnection(_sqlSettings.TestsMasterConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                                        IF DB_ID('BrighterTests') IS NULL
                                        BEGIN
                                            CREATE DATABASE BrighterTests;
                                        END;";
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

        public MsSqlCommandStoreConfiguration CommandStoreConfiguration => new MsSqlCommandStoreConfiguration(_sqlSettings.TestsBrighterConnectionString, _tableName);

        public MsSqlMessageStoreConfiguration MessageStoreConfiguration => new MsSqlMessageStoreConfiguration(_sqlSettings.TestsBrighterConnectionString, _tableName);

        public void CleanUpDb()
        {
            using (var connection = new SqlConnection(_sqlSettings.TestsBrighterConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
                                        IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{_tableName}') AND type in (N'U'))
                                        BEGIN
                                            DROP TABLE {_tableName}
                                        END;";
                    command.ExecuteNonQuery();
                }
            }
        }

        public void CreateMessageStoreTable()
        {
            using (var connection = new SqlConnection(_sqlSettings.TestsBrighterConnectionString))
            {
                _tableName = $"[message_{_tableName}]";
                var createTableSql = SqlMessageStoreBuilder.GetDDL(_tableName);

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
            using (var connection = new SqlConnection(_sqlSettings.TestsBrighterConnectionString))
            {
                _tableName = $"[command_{_tableName}]";
                var createTableSql = SqlCommandStoreBuilder.GetDDL(_tableName);

                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = createTableSql;
                    command.ExecuteNonQuery();
                }
            }
        }
    }

    internal class SqlSettings
    {
        public string TestsBrighterConnectionString { get; set; } =
            "Server=.;Database=BrighterTests;Integrated Security=True;Application Name=BrighterTests";

        public string TestsMasterConnectionString { get; set; } =
            "Server=.;Database=master;Integrated Security=True;Application Name=BrighterTests";
    }
}