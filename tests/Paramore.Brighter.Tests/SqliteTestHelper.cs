using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;

namespace Paramore.Brighter.Tests
{
    public class SqliteTestHelper
    {
        private const string TestDbPath = "test.db";
        public string ConnectionString = $"DataSource=\"{TestDbPath}\"";
        public string TableName = "test_commands";
        public string TableName_Messages = "test_messages";
        private string connectionStringPath;
        private string connectionStringPathDir;

        public void SetupCommandDb()
        {
            connectionStringPath = GetUniqueTestDbPathAndCreateDir();
            ConnectionString = $"DataSource=\"{connectionStringPath}\"";
            CreateDatabaseWithTable(ConnectionString, SqliteInboxBuilder.GetDDL(TableName));
        }

        public void SetupMessageDb()
        {
            connectionStringPath = GetUniqueTestDbPathAndCreateDir();
            ConnectionString = $"DataSource=\"{connectionStringPath}\"";
            CreateDatabaseWithTable(ConnectionString, SqliteOutboxBuilder.GetDDL(TableName_Messages));
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
                //_sqlConnection?.Close();
                //_sqlConnection?.Dispose();
                //GC.Collect();  // Otherwise we can find the file handle still in use when we delete the file
                //GC.WaitForPendingFinalizers();
                //GC.Collect();

                File.Delete(connectionStringPath);
                Directory.Delete(connectionStringPathDir, true);
            }
            catch (Exception e)
            {                
                Console.WriteLine($"{e.Message}{Environment.NewLine}{e.StackTrace}");
                throw;
            }
        }

        private void CreateDatabaseWithTable(string dataSourceTestDb, string createTableScript)
        {
            using (var sqliteConnection = new SqliteConnection(dataSourceTestDb))
            {
                using (var command = sqliteConnection.CreateCommand())
                {
                    command.CommandText = createTableScript;

                    sqliteConnection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
