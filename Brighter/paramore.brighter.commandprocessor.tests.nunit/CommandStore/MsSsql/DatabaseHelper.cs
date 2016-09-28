using Microsoft.Data.Sqlite;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandStore.MsSsql
{
    public class DatabaseHelper
    {
        public static SqliteConnection CreateDatabaseWithTable(string dataSourceTestDb, string createTableScript)
        {
            var sqliteConnection = new SqliteConnection(dataSourceTestDb);

            sqliteConnection.Open();
            using (var command = sqliteConnection.CreateCommand())
            {
                command.CommandText = createTableScript;
                command.ExecuteNonQuery();
            }

            return sqliteConnection;
        }
    }
}