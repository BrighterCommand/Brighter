using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.MessagingGateway.Postgres;

/// <summary>
/// Base class for PostgreSQL messaging gateway components, providing shared functionality
/// for interacting with the PostgreSQL database for message queuing.
/// </summary>
public class PostgresMessagingGateway(PostgresMessagingGatewayConnection connection)
{
    /// <summary>
    /// Gets the connection details for the PostgreSQL messaging gateway.
    /// </summary>
    protected PostgresMessagingGatewayConnection Connection { get; } = connection;
    
    /// <summary>
    /// Provides access to PostgreSQL database connections based on the configured connection string.
    /// </summary>
    protected PostgreSqlConnectionProvider ConnectionProvider { get; } = new(connection.Configuration);
    
    /// <summary>
    /// Asynchronously ensures that the queue store table exists in the PostgreSQL database.
    /// It can either create the table if it's missing or check for its existence and throw an exception if it doesn't exist,
    /// depending on the <paramref name="makeTable"/> setting.
    /// </summary>
    /// <param name="schemaName">The schema name where the queue table should reside.</param>
    /// <param name="tableName">The name of the queue store table.</param>
    /// <param name="binaryMessagePayload">A flag indicating whether the message content should be stored as JSONB (binary JSON) or JSON.</param>
    /// <param name="makeTable">An <see cref="OnMissingChannel"/> enum value indicating how to handle a missing table.</param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <exception cref="ConfigurationException">Thrown if <paramref name="makeTable"/> is <see cref="OnMissingChannel.Create"/> and table creation fails,
    /// or if <paramref name="makeTable"/> is <see cref="OnMissingChannel.Validate"/> and the table does not exist.</exception>
    protected async Task EnsureQueueStoreExistsAsync(string schemaName, 
        string tableName,
        bool binaryMessagePayload,
        OnMissingChannel makeTable,
        CancellationToken cancellation)
    {
        if (makeTable == OnMissingChannel.Assume)
        {
            return;
        }
        
        await using var connection = await ConnectionProvider.GetConnectionAsync(cancellation);
        if (makeTable == OnMissingChannel.Create)
        {
            var column = binaryMessagePayload ? "JSONB" : "JSON";
            await using var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = $"""
                                  CREATE TABLE IF NOT EXISTS "{schemaName}"."{tableName}"
                                  (
                                      "id" BIGINT GENERATED ALWAYS AS IDENTITY,
                                      "visible_timeout" TIMESTAMPTZ,
                                      "queue" VARCHAR(255),
                                      "content" {column}
                                  )
                                  """;
            await createTableCommand.ExecuteNonQueryAsync(cancellation);
            
            await using var createIndexCommand = connection.CreateCommand();
            createIndexCommand.CommandText = $"""
                                               CREATE INDEX IF NOT EXISTS "{schemaName}_{tableName}_queue_visible_timeout_idx" ON "{schemaName}"."{tableName}"("queue", "visible_timeout") INCLUDE ("id")
                                               """;
            await createIndexCommand.ExecuteNonQueryAsync(cancellation);
        }
        else
        {
            await using var checkIfTableExistsCommand = connection.CreateCommand();
            checkIfTableExistsCommand.CommandText = """
                                                     SELECT EXISTS(
                                                        SELECT FROM information_schema.tables 
                                                        WHERE  table_schema = $1
                                                        AND    table_name   = $2)
                                                     """;

            checkIfTableExistsCommand.Parameters.Add(new NpgsqlParameter { Value = schemaName });
            checkIfTableExistsCommand.Parameters.Add(new NpgsqlParameter { Value = tableName });
            var exists = await checkIfTableExistsCommand.ExecuteScalarAsync(cancellation);
            if (!Convert.ToBoolean(exists))
            {
                throw new ConfigurationException($"The queue store (\"{schemaName}\".\"{tableName}\") doesn't not exists");
            }
        }
    }
    
    /// <summary>
    /// Synchronously ensures that the queue store table exists in the PostgreSQL database.
    /// It can either create the table if it's missing or check for its existence and throw an exception if it doesn't exist,
    /// depending on the <paramref name="makeTable"/> setting.
    /// </summary>
    /// <param name="schemaName">The schema name where the queue table should reside.</param>
    /// <param name="tableName">The name of the queue store table.</param>
    /// <param name="binaryMessagePayload">A flag indicating whether the message content should be stored as JSONB (binary JSON) or JSON.</param>
    /// <param name="makeTable">An <see cref="OnMissingChannel"/> enum value indicating how to handle a missing table.</param>
    /// <exception cref="ConfigurationException">Thrown if <paramref name="makeTable"/> is <see cref="OnMissingChannel.Create"/> and table creation fails,
    /// or if <paramref name="makeTable"/> is <see cref="OnMissingChannel.Validate"/> and the table does not exist.</exception>
    protected void EnsureQueueStoreExists(string schemaName, 
        string tableName,
        bool binaryMessagePayload,
        OnMissingChannel makeTable)
    {
        if (makeTable == OnMissingChannel.Assume)
        {
            return;
        }
        
        using var connection = ConnectionProvider.GetConnection();
        if (makeTable == OnMissingChannel.Create)
        {
            var column = binaryMessagePayload ? "JSONB" : "JSON";
            using var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = $"""
                                  CREATE TABLE IF NOT EXISTS "{schemaName}"."{tableName}"
                                  (
                                      "id" BIGINT GENERATED ALWAYS AS IDENTITY,
                                      "visible_timeout" TIMESTAMPTZ,
                                      "queue" VARCHAR(255),
                                      "content" {column}
                                  )
                                  """;
            createTableCommand.ExecuteNonQuery();
            
            using var createIndexCommand = connection.CreateCommand();
            createIndexCommand.CommandText = $"""
                                               CREATE INDEX IF NOT EXISTS "{schemaName}_{tableName}_queue_visible_timeout_idx" ON "{schemaName}"."{tableName}"("queue", "visible_timeout") INCLUDE ("id")
                                               """;
            createIndexCommand.ExecuteNonQuery();
        }
        else
        {
            using var checkIfTableExistsCommand = connection.CreateCommand();
            checkIfTableExistsCommand.CommandText = """
                                                     SELECT EXISTS(
                                                        SELECT FROM information_schema.tables 
                                                        WHERE  table_schema = $1
                                                        AND    table_name   = $2)
                                                     """;

            checkIfTableExistsCommand.Parameters.Add(new NpgsqlParameter { Value = schemaName });
            checkIfTableExistsCommand.Parameters.Add(new NpgsqlParameter { Value = tableName });
            var exists = checkIfTableExistsCommand.ExecuteScalar();
            if (!Convert.ToBoolean(exists))
            {
                throw new ConfigurationException($"The queue store (\"{schemaName}\".\"{tableName}\") doesn't not exists");
            }
        }
    }
}
