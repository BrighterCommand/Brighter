using System;
using System.Data.SqlClient;
using Paramore.Brighter.Commandstore.MsSql;
using Paramore.Brighter.Messagestore.MsSql;

namespace Paramore.Brighter.Tests
{
    public class MsSqlTestHelper
    {
        private const string ConnectionString = "Server=.;Database=BrighterTests;Integrated Security=True;Application Name=BrighterTests";
        private string _tableName;

        public MsSqlTestHelper()
        {
            _tableName = "test_" + Guid.NewGuid();
        }

        public MsSqlTestHelper(string tableName) : this()
        {
            _tableName = tableName;
        }

        public void CreateDatabase()
        {
            using (var connection = new SqlConnection("Server=.;Database=master;Integrated Security=True;Application Name=BrighterTests"))
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

        public MsSqlCommandStoreConfiguration CommandStoreConfiguration
        {
            get
            {
                return new MsSqlCommandStoreConfiguration(ConnectionString, _tableName);
            }
        }

        public MsSqlMessageStoreConfiguration MessageStoreConfiguration
        {
            get
            {
                return new MsSqlMessageStoreConfiguration(ConnectionString, _tableName, MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer);
            }
        }

        public void CleanUpDb()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = String.Format(@"
                                        IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[{0}]') AND type in (N'U'))
                                        BEGIN
                                            DROP TABLE [{0}]
                                        END;", _tableName);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void CreateMessageStoreTable()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                _tableName = "message_" + _tableName;
                string createTableSql = SqlMessageStoreBuilder.GetDDL(_tableName);

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
                _tableName = "command_" + _tableName;
                string createTableSql = SqlCommandStoreBuilder.GetDDL(_tableName);

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