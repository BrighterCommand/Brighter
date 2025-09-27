using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using Google.Cloud.Spanner.Data;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Spanner;

namespace Paramore.Brighter.Outbox.Spanner;

/// <summary>
/// Implements the Brighter outbox pattern using Google Cloud Spanner.
/// This class extends <see cref="RelationDatabaseOutbox"/> to provide a reliable
/// mechanism for storing and tracking messages before they are dispatched to a message broker.
/// It ensures atomicity between the business transaction and message persistence.
/// </summary>
/// <remarks>
/// This outbox leverages Google Spanner's strong consistency and transactional capabilities
/// to guarantee that messages are durably saved to the outbox table. This prevents
/// message loss in scenarios where the application might fail after committing a
/// business transaction but before the message is successfully published.
/// <para>
/// It relies on a configured Spanner database and the provided connection provider
/// to interact with the outbox table. The <see cref="SpannerQueries"/> class
/// encapsulates the Spanner-specific SQL queries for outbox operations.
/// </para>
/// </remarks>
public class SpannerOutbox(IAmARelationalDatabaseConfiguration configuration, IAmARelationalDbConnectionProvider connectionProvider) 
    : RelationDatabaseOutbox(DbSystem.Spanner, configuration, connectionProvider, new SpannerQueries(), s_logger)
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SpannerOutbox>();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SpannerOutbox"/> class with only
    /// the database configuration. This constructor internally creates a default
    /// <see cref="SpannerConnectionProvider"/> based on the provided configuration.
    /// </summary>
    /// <param name="configuration">The configuration settings specific to the relational database,
    /// including connection string, database name, and outbox table name.</param>
    public SpannerOutbox(IAmARelationalDatabaseConfiguration configuration)
        : this(configuration, new SpannerConnectionProvider(configuration))
    {
        
    }
    
    /// <inheritdoc />
    protected override DbCommand CreateCommand(DbConnection connection, string sqlText, int outBoxTimeout,
        params IDbDataParameter[] parameters)
    {
        var command = connection.CreateCommand();

        // Spanner doesn't accept timeout as 0, so we are going to set the default value as 60
        command.CommandTimeout = outBoxTimeout <= 0 ? 60 : outBoxTimeout;
        command.CommandText = sqlText;
        command.Parameters.AddRange(parameters);

        return command;
    }

    /// <inheritdoc />
    protected override DbCommand InitMarkDispatchedCommand(DbConnection connection, Id messageId, DateTimeOffset? dispatchedAt)
        => CreateCommand(connection, GenerateSqlText(Queries.MarkDispatchedCommand), 0,
            CreateSqlParameter("MessageId", DbType.String, messageId.Value),
            CreateSqlParameter("DispatchedAt", DbType.DateTimeOffset, dispatchedAt?.ToUniversalTime()));

    /// <inheritdoc />
    protected override IDbDataParameter[] CreatePagedDispatchedParameters(TimeSpan dispatchedSince, int pageSize, int pageNumber)
    {
        var parameters = new IDbDataParameter[3];
        parameters[0] = CreateSqlParameter("@Skip", DbType.Int32, Math.Max(pageNumber - 1, 0) * pageSize);
        parameters[1] = CreateSqlParameter("@Take", DbType.Int32, pageSize);
        parameters[2] = CreateSqlParameter("@DispatchedSince", DbType.DateTimeOffset, DateTimeOffset.UtcNow.Subtract(dispatchedSince));

        return parameters;
    }

    protected override IDbDataParameter[] CreatePagedOutstandingParameters(TimeSpan since, int pageSize, int pageNumber,
        IDbDataParameter[] inParams)
    {
        var parameters = new IDbDataParameter[3];
        parameters[0] = CreateSqlParameter("@Skip", DbType.Int32, Math.Max(pageNumber - 1, 0) * pageSize);
        parameters[1] = CreateSqlParameter("@Take", DbType.Int32, pageSize);
        parameters[2] = CreateSqlParameter("@TimestampSince", DbType.DateTimeOffset, DateTimeOffset.UtcNow.Subtract(since));

        return parameters.Concat(inParams).ToArray();
    }

    /// <inheritdoc />
    protected override bool IsExceptionUniqueOrDuplicateIssue(Exception ex) 
        => ex is SpannerException se && se.RpcException.StatusCode == StatusCode.AlreadyExists;

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value) 
        => new SpannerParameter { ParameterName = parameterName, Value = value ?? DBNull.Value };

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, DbType dbType, object? value)
    {
        var spannerType = ToSpannerDbType(dbType);
        if (value is DateTimeOffset dateTimeOffset)
        {
            value = dateTimeOffset.DateTime;
        }
        
        return new SpannerParameter(parameterName, spannerType, value ?? DBNull.Value);
    }

    private static SpannerDbType ToSpannerDbType(DbType dbType)
    {
        return dbType switch
        {
            DbType.String  => SpannerDbType.String,
            DbType.DateTimeOffset => SpannerDbType.Timestamp,
            DbType.Binary => SpannerDbType.Bytes,
            DbType.Int32 => SpannerDbType.Int64,
            _ => throw new ArgumentOutOfRangeException(nameof(dbType), dbType, null)
        };
    }

    protected override bool TryGetOrdinal(DbDataReader dr, string columnName, out int ordinal)
    {
        try
        {
            return base.TryGetOrdinal(dr, columnName, out ordinal);
        }
        catch (KeyNotFoundException)
        {
            ordinal = -1;
            return false;
        }
    }
}
