using System;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace Paramore.Brighter.Oracle.Tests.BoxProvisioning;

internal static class OracleBoxProvisioningTestHelper
{
    public static async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = """
                              SELECT COUNT(1) FROM ALL_TABLES
                              WHERE OWNER = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                                AND TABLE_NAME = UPPER(:TableName)
                              """;
        command.Parameters.Add(new OracleParameter("TableName", tableName));

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        return count > 0;
    }

    public static async Task<int> HistoryCountForVersionAsync(string connectionString, string tableName, int version)
    {
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = """
                              SELECT COUNT(1) FROM BRIGHTER_MIGRATION_HISTORY
                              WHERE BoxTableName = :BoxTableName
                                AND SchemaName = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                                AND MigrationVersion = :MigrationVersion
                              """;
        command.Parameters.Add(new OracleParameter("BoxTableName", tableName));
        command.Parameters.Add(new OracleParameter("MigrationVersion", version));

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public static async Task DropTableIfExistsAsync(string connectionString, string tableName)
    {
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = """
                              BEGIN
                                EXECUTE IMMEDIATE 'DROP TABLE ' || :TableName;
                              EXCEPTION
                                WHEN OTHERS THEN
                                  IF SQLCODE != -942 THEN
                                    RAISE;
                                  END IF;
                              END;
                              """;
        command.Parameters.Add(new OracleParameter("TableName", tableName));

        await command.ExecuteNonQueryAsync();
    }
}
