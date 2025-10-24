using System;
using System.Data;
using System.Data.Common;
using Google.Cloud.Spanner.Data;
using Grpc.Core;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Spanner;

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
public class SpannerInboxAsync(
    IAmARelationalDatabaseConfiguration configuration,
    IAmARelationalDbConnectionProvider connectionProvider)
    : RelationalDatabaseInbox(DbSystem.Spanner, configuration, connectionProvider,
        new SpannerSqlQueries(), ApplicationLogging.CreateLogger<SpannerInboxAsync>())
{
    public SpannerInboxAsync(IAmARelationalDatabaseConfiguration configuration)
        : this(configuration, new SpannerConnectionProvider(configuration))
    {
        
    }
    
    /// <inheritdoc />
    protected override DbCommand CreateCommand(DbConnection connection, string sqlText, int outBoxTimeout,
        params IDbDataParameter[] parameters)
    {

        var command = connection.CreateCommand();

        // Spanner doesn't accept timeout as 0, so we are going to set the default value as 60
        command.CommandTimeout = outBoxTimeout < 0 ? 60 : outBoxTimeout;
        command.CommandText = sqlText;
        command.Parameters.AddRange(parameters);

        return command;
    }

    /// <inheritdoc />
    protected override bool IsExceptionUniqueOrDuplicateIssue(Exception ex)
        => ex is SpannerException se && se.RpcException.StatusCode == StatusCode.AlreadyExists;

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
    {
        if (parameterName == "@CommandBody")
        {
            return new SpannerParameter 
            {
                ParameterName = parameterName, 
                SpannerDbType = SpannerDbType.Json,
                Value = value ?? DBNull.Value 
            };
        }
        
        return new SpannerParameter { ParameterName = parameterName, Value = value ?? DBNull.Value };
    }

    protected override IDbDataParameter[] CreateGetParameters(string commandId, string contextKey)
    {
        return
        [
            new SpannerParameter("@CommandID", SpannerDbType.String, commandId),
            new SpannerParameter("@ContextKey", SpannerDbType.String, contextKey),
        ];
    }
}
