using Npgsql;
using Paramore.Brighter.MessageStore.PostgreSql;
using System;
using System.Collections.Generic;
using System.Text;

namespace Paramore.Brighter.Tests
{
    class PostgreSqlTestHelper
    {
        //SSPI Authentication is Not allowed, Username and Password should be added to run tests
        private const string ConnectionString = "Server=localhost;Port=5432;Database=postgres;User Id={username};Password={password};";
        private string _tableName;

        public PostgreSqlTestHelper()
        {
            _tableName = $"test_{Guid.NewGuid().ToString("N")}";
        }
        public PostgreSqlMessageStoreConfiguration MessageStoreConfiguration => new PostgreSqlMessageStoreConfiguration(ConnectionString, _tableName);

        public void CleanUpTable()
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
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
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                _tableName = $"message_{_tableName}";
                var createTableSql = PostgreSqlMessageStoreBulder.GetDDL(_tableName);

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
