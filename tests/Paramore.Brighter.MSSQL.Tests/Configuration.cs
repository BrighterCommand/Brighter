using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MSSQL.Tests;

public static class Configuration
{
    public const string DefaultConnectingString = "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password123!;Application Name=BrighterTests;Connect Timeout=60;Encrypt=false";
    public const string TablePrefix = "Table";

    private static bool s_databaseCreated;
    private static readonly SemaphoreSlim s_semaphoreSlim = new(1, 1);
    
    public static void EnsureDatabaseExists(string connectionString)
    {
        if (s_databaseCreated)
        {
            return;
        }
        
        s_semaphoreSlim.Wait();
        try
        {
            if (s_databaseCreated)
            {
                return;
            }

            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;
            builder.InitialCatalog = "master";

            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                 IF DB_ID('{databaseName}') IS NULL
                 BEGIN
                     CREATE DATABASE {databaseName};
                 END;
                 """;
            command.ExecuteNonQuery();
        }
        finally
        {
            s_databaseCreated = true;
            s_semaphoreSlim.Release();
        }
    }
    
    public static async Task EnsureDatabaseExistsAsync(string connectionString)
    {
        if (s_databaseCreated)
        {
            return;
        }
        
        await s_semaphoreSlim.WaitAsync();
        try
        {
            if (s_databaseCreated)
            {
                return;
            }

            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;
            builder.InitialCatalog = "master";

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                 IF DB_ID('{databaseName}') IS NULL
                 BEGIN
                     CREATE DATABASE {databaseName};
                 END;
                 """;
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            s_databaseCreated = true;
            s_semaphoreSlim.Release();
        }
    }

    public static void CreateTable(string connectionString, string ddl)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = ddl;
        command.ExecuteNonQuery();
    }
    
    public static async Task CreateTableAsync(string connectionString, string ddl)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = ddl;
        await command.ExecuteNonQueryAsync();
    }
    
    public static void DeleteTable(string connectionString, string tableName)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {tableName}";
        command.ExecuteNonQuery();
    }
    
    public static async Task DeleteTableAsync(string connectionString, string tableName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {tableName}";
        await command.ExecuteNonQueryAsync();
    }
}
