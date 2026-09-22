#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MSSQL.Tests;

public static class Configuration
{
    public const string DefaultConnectingString = "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password123!;Application Name=BrighterTests;Connect Timeout=60;Encrypt=false";
    public const string TablePrefix = "Table";

    private const string ENSURE_DATABASE_SQL = """
        IF DB_ID(@databaseName) IS NOT NULL RETURN;

        DECLARE @lockResult int;
        EXEC @lockResult = sys.sp_getapplock
            @Resource = N'Brighter.Tests.CreateDatabase',
            @LockMode = 'Exclusive',
            @LockOwner = 'Session',
            @LockTimeout = 30000;

        IF @lockResult < 0
            THROW 50000, 'Could not acquire the test database creation lock.', 1;

        IF DB_ID(@databaseName) IS NULL
            EXEC (@createDatabaseSql);
        """;

    /// <summary>
    /// Ensures the database exists, coordinating creation with other test processes through SQL Server.
    /// </summary>
    /// <param name="connectionString">The connection string identifying the test database.</param>
    public static void EnsureDatabaseExists(string connectionString)
    {
        using var connection = CreateMasterConnection(connectionString, out string databaseName);
        connection.Open();
        using var command = CreateDatabaseCommand(connection, databaseName);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Ensures the database exists asynchronously, coordinating creation with other test processes through SQL Server.
    /// </summary>
    /// <param name="connectionString">The connection string identifying the test database.</param>
    /// <returns>A task that completes when the database exists.</returns>
    public static async Task EnsureDatabaseExistsAsync(string connectionString)
    {
        await using var connection = CreateMasterConnection(connectionString, out string databaseName);
        await connection.OpenAsync();
        await using var command = CreateDatabaseCommand(connection, databaseName);
        await command.ExecuteNonQueryAsync();
    }

    private static SqlConnection CreateMasterConnection(string connectionString, out string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";
        // A physical disconnect releases the session lock even when a command fails or times out.
        builder.Pooling = false;
        // CREATE DATABASE must execute outside a transaction.
        builder.Enlist = false;
        return new SqlConnection(builder.ConnectionString);
    }

    private static SqlCommand CreateDatabaseCommand(SqlConnection connection, string databaseName)
    {
        using var builder = new SqlCommandBuilder();
        var command = connection.CreateCommand();
        command.CommandText = ENSURE_DATABASE_SQL;
        command.CommandTimeout = 60;
        command.Parameters.Add("@databaseName", SqlDbType.NVarChar, -1).Value = databaseName;
        command.Parameters.Add("@createDatabaseSql", SqlDbType.NVarChar, -1).Value =
            $"CREATE DATABASE {builder.QuoteIdentifier(databaseName)}";
        return command;
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
