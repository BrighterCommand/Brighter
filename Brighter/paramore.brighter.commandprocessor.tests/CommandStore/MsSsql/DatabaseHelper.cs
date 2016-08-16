using Microsoft.Data.Sqlite;
using paramore.brighter.commandprocessor.commandstore.sqllite;

namespace paramore.commandprocessor.tests.CommandStore.MsSsql
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