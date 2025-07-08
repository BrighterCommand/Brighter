using System;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Inbox.Spanner;

/// <summary>
/// Implements the <see cref="IAmAnInbox"/> pattern for Google Cloud Spanner databases,
/// integrating with the Brighter framework. This class extends <see cref="RelationalDatabaseInbox"/>
/// to provide an inbox mechanism for message de-duplication and idempotency
/// when consuming messages, ensuring that messages are processed exactly once.
/// </summary>
/// <remarks>
/// This concrete implementation leverages Spanner's strong consistency guarantees to
/// reliably store and manage message processing state. It utilizes the underlying
/// <see cref="RelationalDatabaseInbox"/>'s logic but adapts it for Spanner's
/// specific ADO.NET provider (<see cref="Google.Cloud.Spanner.Data"/>).
/// <para>
/// When using this inbox, ensure your Spanner database schema for the inbox table
/// is compatible with the Brighter framework's requirements (e.g., columns for
/// message ID, message type, timestamp, etc.). The <see cref="RelationalDatabaseInbox"/>
/// base class handles much of the common database interaction logic.
/// </para>
/// </remarks>
public class SpannerInbox(
    IAmARelationalDbConnectionProvider connectionProvider,
    IAmARelationalDatabaseConfiguration configuration)
    : RelationalDatabaseInbox(DbSystem.Spanner, configuration.DatabaseName, configuration.InBoxTableName,
        new SpannerSqlQueries(), ApplicationLogging.CreateLogger<SpannerInbox>())
{
    /// <inheritdoc />
    protected override void WriteToStore(Func<DbConnection, DbCommand> commandFunc, Action? loggingAction)
    {
        using var connection = GetOpenConnection(connectionProvider);
        using var command = commandFunc.Invoke(connection);
        
        try
        {
            command.ExecuteNonQuery();
        }
        catch (SpannerException ex)
        {
            if (ex.ErrorCode == ErrorCode.AlreadyExists)
            {
                loggingAction?.Invoke();
                return;
            }
            
            throw;
        }
    }

    /// <inheritdoc />
    protected override async Task WriteToStoreAsync(Func<DbConnection, DbCommand> commandFunc, Action? loggingAction, CancellationToken cancellationToken)
    {
#if NETSTANDARD
        using var connection = await GetOpenConnectionAsync(connectionProvider, cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);
        
        using var command = commandFunc.Invoke(connection);
#else
        await using var connection = await GetOpenConnectionAsync(connectionProvider, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        
        await using var command = commandFunc.Invoke(connection);
#endif
        
        try
        {
            await command
                .ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        catch (SpannerException ex)
        {
            if (ex.ErrorCode == ErrorCode.AlreadyExists)
            {
                loggingAction?.Invoke();
                return;
            }
            
            throw;
        }
    }

    /// <inheritdoc />
    protected override T ReadFromStore<T>(Func<DbConnection, DbCommand> commandFunc, Func<DbDataReader, string, T> resultFunc, string commandId)
    {
        using var connection = GetOpenConnection(connectionProvider);
        using var command = commandFunc.Invoke(connection);

        var result = command.ExecuteReader();
        return resultFunc.Invoke(result, commandId);
    }

    /// <inheritdoc />
    protected override async Task<T> ReadFromStoreAsync<T>(Func<DbConnection, DbCommand> commandFunc, Func<DbDataReader, string, CancellationToken, Task<T>> resultFunc, string commandId, CancellationToken cancellationToken)
    {
#if NETSTANDARD
        using var connection = await GetOpenConnectionAsync(connectionProvider, cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);

        using var command = commandFunc.Invoke(connection);
#else
        await using var connection = await GetOpenConnectionAsync(connectionProvider, cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);
        
        await using var command = commandFunc.Invoke(connection);
#endif

        var result = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);
        return await resultFunc.Invoke(result, commandId, cancellationToken);
    }

    /// <inheritdoc />
    protected override DbCommand CreateCommand(DbConnection connection, string sqlText, int outBoxTimeout,
        params IDbDataParameter[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
        command.CommandText = sqlText;
        command.Parameters.AddRange(parameters);

        return command;
    }

    /// <inheritdoc />
    protected override IDbDataParameter[] CreateAddParameters<T>(T command, string contextKey)
    {
        var commandJson = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
        return
        [
            CreateSpannerParameter("CommandID", SpannerDbType.String,  command.Id.Value),
            CreateSpannerParameter("CommandType", SpannerDbType.String, typeof (T).Name),
            CreateSpannerParameter("CommandBody", SpannerDbType.Json, commandJson),
            CreateSpannerParameter("Timestamp", SpannerDbType.Timestamp, DateTimeOffset.UtcNow),
            CreateSpannerParameter("ContextKey", SpannerDbType.String, contextKey)
        ];
    }

    /// <inheritdoc />
    protected override IDbDataParameter[] CreateExistsParameters(string commandId, string contextKey)
    {
        return
        [
            CreateSpannerParameter("CommandId", SpannerDbType.String, commandId),
            CreateSpannerParameter("ContextKey", SpannerDbType.String, contextKey)
        ];
    }

    /// <inheritdoc />
    protected override IDbDataParameter[] CreateGetParameters(string commandId, string contextKey)
    {
        return
        [
            CreateSpannerParameter("CommandId", SpannerDbType.String, commandId),
            CreateSpannerParameter("ContextKey", SpannerDbType.String, contextKey)
        ];
    }

    /// <inheritdoc />
    protected override T MapFunction<T>(DbDataReader dr, string commandId)
    {
        try
        {
            if (dr.Read())
            {
                var body = dr.GetString(dr.GetOrdinal("CommandBody"));
                return JsonSerializer.Deserialize<T>(body, JsonSerialisationOptions.Options)!;
            }
        }
        finally
        {
            dr.Close();
        }
        
        throw new RequestNotFoundException<T>(commandId);
    }

    /// <inheritdoc />
    protected override async Task<T> MapFunctionAsync<T>(DbDataReader dr, string commandId, CancellationToken cancellationToken)
    {
        try
        {
            if (await dr.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
            {
                var body = dr.GetString(dr.GetOrdinal("CommandBody"));
                return JsonSerializer.Deserialize<T>(body, JsonSerialisationOptions.Options)!;
            }
        }
        finally
        {
#if NETSTANDARD
            dr.Close();
#else

            await dr.CloseAsync()
                .ConfigureAwait(ContinueOnCapturedContext);
#endif

        }
        
        throw new RequestNotFoundException<T>(commandId);
    }

    /// <inheritdoc />
    protected override bool MapBoolFunction(DbDataReader dr, string commandId)
    {
        try
        {
            return dr.HasRows;
        }
        finally
        {
            dr.Close();
        }
    }

    /// <inheritdoc />
#if NETSTANDARD
    protected override Task<bool> MapBoolFunctionAsync(DbDataReader dr, string commandId, CancellationToken cancellationToken)
    {
        try
        {
            return Task.FromResult(dr.HasRows);
        }
        finally
        {
            dr.Close();
        }
    }
#else
    protected override async Task<bool> MapBoolFunctionAsync(DbDataReader dr, string commandId, CancellationToken cancellationToken)
    {
        try
        {
            return dr.HasRows;
        }
        finally
        {
            await dr.CloseAsync()
                .ConfigureAwait(ContinueOnCapturedContext);
        }
    }
#endif

    private static SpannerParameter CreateSpannerParameter(string parameterName, SpannerDbType type, object? value) 
        => new(parameterName, type, value ?? DBNull.Value);
}
