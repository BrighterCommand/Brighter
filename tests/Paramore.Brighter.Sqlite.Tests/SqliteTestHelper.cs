using System;
using System.IO;
using System.Threading.Tasks;
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

        public RelationalDatabaseConfiguration InboxConfiguration => new(ConnectionString, inboxTableName: InboxTableName, binaryMessagePayload: _binaryMessagePayload);

        public RelationalDatabaseConfiguration OutboxConfiguration => new(ConnectionString, outBoxTableName: OutboxTableName, binaryMessagePayload: _binaryMessagePayload);

        public SqliteTestHelper(bool binaryMessagePayload = false)
        {
            _binaryMessagePayload = binaryMessagePayload;
        }

        public void SetupCommandDb()
        {
            _connectionStringPath = GetUniqueTestDbPathAndCreateDir();
            ConnectionString = $"DataSource=\"{_connectionStringPath}\"";
            CreateDatabaseWithTable(ConnectionString, SqliteInboxBuilder.GetDDL(InboxTableName, _binaryMessagePayload));
        }

        public void SetupMessageDb()
        {
            _connectionStringPath = GetUniqueTestDbPathAndCreateDir();
            ConnectionString = $"DataSource=\"{_connectionStringPath}\"";
            CreateDatabaseWithTable(ConnectionString, SqliteOutboxBuilder.GetDDL(OutboxTableName, hasBinaryMessagePayload: _binaryMessagePayload));
        }

        private string GetUniqueTestDbPathAndCreateDir()
        {
            var testRootPath = Directory.GetCurrentDirectory();
            var guidInPath = Guid.NewGuid().ToString();
            _connectionStringPathDir = Path.Combine(Path.Combine(Path.Combine(testRootPath, "bin"), "TestResults"), guidInPath);
            Directory.CreateDirectory(_connectionStringPathDir);
            return Path.Combine(_connectionStringPathDir, $"test{guidInPath}.db");
        }

        public async Task CleanUpDbAsync()
        {
            try
            {
                //add 1 MS delay to allow the file to be released
                await Task.Delay(1);
                File.Delete(_connectionStringPath);
                Directory.Delete(_connectionStringPathDir, true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"{e.Message}{Environment.NewLine}{e.StackTrace}");
                throw;
            }
        }

        private void CreateDatabaseWithTable(string dataSourceTestDb, string createTableScript)
        {
            using var sqliteConnection = new SqliteConnection(dataSourceTestDb);
            using var command = sqliteConnection.CreateCommand();
            command.CommandText = createTableScript;

            sqliteConnection.Open();
            command.ExecuteNonQuery();
        }
    }
}
