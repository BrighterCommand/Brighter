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
        public readonly string TableName = "test_commands";
        public readonly string TableNameMessages = "test_messages";
        private string _connectionStringPath;
        private string _connectionStringPathDir;

        public SqliteTestHelper(bool binaryMessagePayload = false)
        {
            _binaryMessagePayload = binaryMessagePayload;
        }

        public void SetupCommandDb()
        {
            _connectionStringPath = GetUniqueTestDbPathAndCreateDir();
            ConnectionString = $"DataSource=\"{_connectionStringPath}\"";
            CreateDatabaseWithTable(ConnectionString, SqliteInboxBuilder.GetDDL(TableName));
        }

        public void SetupMessageDb()
        {
            _connectionStringPath = GetUniqueTestDbPathAndCreateDir();
            ConnectionString = $"DataSource=\"{_connectionStringPath}\"";
            CreateDatabaseWithTable(ConnectionString, SqliteOutboxBuilder.GetDDL(TableNameMessages, hasBinaryMessagePayload: _binaryMessagePayload));
        }

        private string GetUniqueTestDbPathAndCreateDir()
        {
            var testRootPath = Directory.GetCurrentDirectory();
            var guidInPath = Guid.NewGuid().ToString();
            _connectionStringPathDir = Path.Combine(Path.Combine(Path.Combine(testRootPath, "bin"), "TestResults"), guidInPath);
            Directory.CreateDirectory(_connectionStringPathDir);
            return Path.Combine(_connectionStringPathDir, $"test{guidInPath}.db");
        }

        public void CleanUpDb()
        {
            try
            {
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
