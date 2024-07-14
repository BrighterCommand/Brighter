using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace DbMaker;

public static class DbConnectionFactory
{
    public static DbConnection GetConnection(Rdbms rdbms, string connectionString)
    {
        return rdbms switch
        {
            Rdbms.MySql => new MySqlConnection(connectionString),
            Rdbms.MsSql => new SqlConnection(connectionString),
            Rdbms.Postgres => new NpgsqlConnection(connectionString),
            Rdbms.Sqlite => new SqliteConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(rdbms), rdbms, null)
        };
    }
}
