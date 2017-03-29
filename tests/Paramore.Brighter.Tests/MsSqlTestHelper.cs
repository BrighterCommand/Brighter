using System;
using System.Data.SqlClient;
using Paramore.Brighter.CommandStore.MsSql;
using Paramore.Brighter.MessageStore.MsSql;

namespace Paramore.Brighter.Tests
{
    public class MsSqlTestHelper
    {
        private const string ConnectionString = "Server=.;Database=BrighterTests;Integrated Security=True;Application Name=BrighterTests";
        private readonly string _tableName;

        public MsSqlTestHelper()
        {
            _tableName = $"test_{Guid.NewGuid():N}";
        }

        public MsSqlTestHelper(string tableName)
        {
            _tableName = tableName;
        }

        public void CreateDatabase()
        {
            using (var connection = new SqlConnection(ConnectionString))
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

        public MsSqlCommandStoreConfiguration CommandStoreConfiguration => new MsSqlCommandStoreConfiguration(ConnectionString, _tableName);

        public MsSqlMessageStoreConfiguration MessageStoreConfiguration => new MsSqlMessageStoreConfiguration(ConnectionString, _tableName, MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer);

        public void CleanUpDb()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
                                        IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[{_tableName}]') AND type in (N'U'))
                                        BEGIN
                                            DROP TABLE [{_tableName}]
                                        END;";
                    command.ExecuteNonQuery();
                }
            }
        }

        public void CreateMessageStoreTable()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var createTableSql = SqlMessageStoreBuilder.GetDDL($"message_{_tableName}");

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
            using (var connection = new SqlConnection(ConnectionString))
            {
                var createTableSql = SqlCommandStoreBuilder.GetDDL($"command_{_tableName}");

                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = createTableSql;
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}