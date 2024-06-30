using System;
using System.Data.Common;
using GreetingsDb;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace GreetingsWeb.Database;

public static class DbConnectionFactory
{
    public static DbConnection GetConnection(DatabaseType databaseType, string connectionString)
    {
        return databaseType switch
        {
            DatabaseType.MySql => new MySqlConnection(connectionString),
            DatabaseType.MsSql => new SqlConnection(connectionString),
            DatabaseType.Postgres => new NpgsqlConnection(connectionString),
            DatabaseType.Sqlite => new SqliteConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(databaseType), databaseType, null)
        };
    }
}
