using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Inbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests;

public static class Configuration
{
    public const string ConnectionString = "DataSource=\"test.db\"";
    public const string TablePrefix = "Table";

    public static void CreateTable(string connectionString, string ddl)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using (var walCommand = connection.CreateCommand())
        {
            walCommand.CommandText = "PRAGMA journal_mode=WAL;";
            walCommand.ExecuteNonQuery();
        }
        using var command = connection.CreateCommand();
        command.CommandText = ddl;
        command.ExecuteNonQuery();
    }

    public static async Task CreateTableAsync(string connectionString, string ddl)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using (var walCommand = connection.CreateCommand())
        {
            walCommand.CommandText = "PRAGMA journal_mode=WAL;";
            await walCommand.ExecuteNonQueryAsync();
        }
        await using var command = connection.CreateCommand();
        command.CommandText = ddl;
        await command.ExecuteNonQueryAsync();
    }
    
    public static void DeleteTable(string connectionString, string tableName)
    {
        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE {tableName}";
            command.ExecuteNonQuery();
        }
        catch
        {
            // Ignoring any error
        }
    }
    
    public static async Task DeleteTableAsync(string connectionString, string tableName)
    {
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE {tableName}";
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // Ignoring any error
        }
    }
}
