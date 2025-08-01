using System;
using System.Data;
using Google.Cloud.Spanner.Data;
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
public class SpannerInbox(
    IAmARelationalDatabaseConfiguration configuration,
    IAmARelationalDbConnectionProvider connectionProvider)
    : RelationalDatabaseInbox(DbSystem.Spanner, configuration, connectionProvider,
        new SpannerSqlQueries(), ApplicationLogging.CreateLogger<SpannerInbox>())
{
    public SpannerInbox(IAmARelationalDatabaseConfiguration configuration)
        : this(configuration, new SpannerConnectionProvider(configuration))
    {
        
    }

    /// <inheritdoc />
    protected override bool IsExceptionUniqueOrDuplicateIssue(Exception ex) 
        => ex is SpannerException { ErrorCode: ErrorCode.AlreadyExists };

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value) 
        => new SpannerParameter { ParameterName = parameterName, Value = value ?? DBNull.Value };
}
