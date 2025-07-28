using System;
using System.Data;
using Google.Cloud.Spanner.Data;
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
    protected override bool IsExceptionUniqueOrDuplicateIssue(Exception ex) 
        => ex is SpannerException { ErrorCode: ErrorCode.AlreadyExists };

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value) 
        => new SpannerParameter { ParameterName = parameterName, Value = value ?? DBNull.Value };

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, DbType dbType, object? value)
        => new SpannerParameter { ParameterName = parameterName, DbType = dbType, Value = value ?? DBNull.Value };
}
