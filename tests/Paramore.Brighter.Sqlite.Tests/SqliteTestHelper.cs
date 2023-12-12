using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests
{
    public class SqliteTestHelper
    {
        private readonly bool _binaryMessagePayload;
        private const string TestDbPath = "test.db";
        public string ConnectionString = $"DataSource=\"{TestDbPath}\"";
        public readonly string InboxTableName = "test_commands";
        public readonly string OutboxTableName = "test_messages";
        private string _connectionStringPath;
        private string _connectionStringPathDir;
        
        public RelationalDatabaseConfiguration InboxConfiguration => new(ConnectionString, inboxTableName: InboxTableName);
        
        public RelationalDatabaseConfiguration OutboxConfiguration => 
                    new(ConnectionString, outBoxTableName: OutboxTableName, binaryMessagePayload: _binaryMessagePayload);

        public SqliteTestHelper(bool binaryMessagePayload = false)
        {
            _binaryMessagePayload = binaryMessagePayload;
        }

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

        public async Task CleanUpDbAsync()
        {
            try
            {
                //add 1 MS delay to allow the file to be released
                await Task.Delay(1);
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
