using System;
using System.IO;
using Microsoft.Data.Sqlite;
using paramore.brighter.commandprocessor.commandstore.sqlite;
using paramore.brighter.commandprocessor.messagestore.mssql;
using paramore.brighter.commandprocessor.messagestore.sqlite;

namespace paramore.brighter.commandprocessor.tests.nunit
{
    public class SqliteTestHelper
    {
        private const string TestDbPath = "test.db";
        public string ConnectionString = "DataSource=\"" + TestDbPath + "\"";
        public string TableName = "test_commands";
        public string TableName_Messages = "test_messages";
        private string connectionStringPath;
        private SqliteConnection _sqlConnection;
        private string connectionStringPathDir;

        public SqliteConnection CreateDatabase()
        {
            connectionStringPath = GetUniqueTestDbPathAndCreateDir();
            ConnectionString = "DataSource=\"" + connectionStringPath + "\"";
            return CreateDatabaseWithTable(ConnectionString, SqliteCommandStoreBuilder.GetDDL(TableName));
        }

        public SqliteConnection CreateMessageStoreConnection()
        {
            connectionStringPath = GetUniqueTestDbPathAndCreateDir();
            ConnectionString = "DataSource=\"" + connectionStringPath + "\"";
            return CreateDatabaseWithTable(ConnectionString, SqliteMessageStoreBuilder.GetDDL(TableName_Messages));
        }

        private string GetUniqueTestDbPathAndCreateDir()
        {
            var testRootPath = Directory.GetCurrentDirectory();
            var guidInPath = Guid.NewGuid().ToString();
            connectionStringPathDir = Path.Combine(Path.Combine(Path.Combine(testRootPath, "bin"), "TestResults"), guidInPath);
            Directory.CreateDirectory(connectionStringPathDir);
            return Path.Combine(connectionStringPathDir, $"test{guidInPath}.db");
        }

        public void CleanUpDb()
        {
            try
            {
                _sqlConnection?.Close();
                _sqlConnection?.Dispose();
                File.Delete(connectionStringPath);
                Directory.Delete(connectionStringPathDir, true);
            }
            catch (Exception e)
            {                
                Console.WriteLine($"{e.Message}{Environment.NewLine}{e.StackTrace}");
                throw;
            }
        }

        private SqliteConnection CreateDatabaseWithTable(string dataSourceTestDb, string createTableScript)
        {
            _sqlConnection = new SqliteConnection(dataSourceTestDb);

            _sqlConnection.Open();
            using (var command = _sqlConnection.CreateCommand())
            {
                command.CommandText = createTableScript;
                command.ExecuteNonQuery();
            }

            return _sqlConnection;
        }
    }
}