using System;
using System.IO;
using Microsoft.Data.Sqlite;
using paramore.brighter.commandprocessor.commandstore.sqllite;
using paramore.brighter.commandprocessor.tests.nunit.CommandStore.MsSsql;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandStore.Sqlite
{
    public class SqlLiteTestHelper
    {
        private const string TestDbPath = "test.db";
        public string ConnectionString = "DataSource=\"" + TestDbPath + "\"";
        public string TableName = "test_messages";
        private static string connectionStringPath;

        public SqliteConnection CreateDatabase()
        {
            connectionStringPath = Path.Combine(Directory.GetCurrentDirectory(), Guid.NewGuid().ToString());
            var dbPath = Path.Combine(connectionStringPath, "test.db");
            ConnectionString = "DataSource=\"" + dbPath + "\"";
            Directory.CreateDirectory(connectionStringPath);
            return DatabaseHelper.CreateDatabaseWithTable(ConnectionString, SqlLiteCommandStoreBuilder.GetDDL(TableName));
        }

        public void CleanUpDb()
        {
            File.Delete(connectionStringPath);
        }
    }
}