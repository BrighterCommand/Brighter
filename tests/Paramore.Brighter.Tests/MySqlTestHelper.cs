using System;
using MySql.Data.MySqlClient;
using Paramore.Brighter.MessageStore.MySql;

namespace Paramore.Brighter.Tests
{
    public class MySqlTestHelper
    {
        public static string ConnectionString = "Server=localhost;Uid=root;Pwd=root;Database=BrighterTests";
        
        private string _tableName;
        public MySqlTestHelper()
        {
            _tableName = $"test_{Guid.NewGuid()}";
        }

       public void CreateDatabase()
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"CREATE DATABASE IF NOT EXISTS `BrighterTests`";
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
            CreateMessageStoreTable();
        }
        
        public MySqlMessageStoreConfiguration MessageStoreConfiguration => new MySqlMessageStoreConfiguration(ConnectionString, _tableName);

        public void CleanUpDb()
        {
            using (var connection = new MySqlConnection(ConnectionString))
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
            using (var connection = new MySqlConnection(ConnectionString))
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
    }
}