namespace Paramore.Brighter.MessagingGateway.Postgres;

/// <summary>
/// Represents the publication configuration specific to PostgreSQL within the Brighter framework.
/// This class extends the base <see cref="Publication"/> class with PostgreSQL-specific settings
/// for customizing how messages are published to a PostgreSQL message queue.
/// </summary>
public class PostgresPublication : Publication
{
    /// <summary>
    /// Gets or sets the schema name where the queue store table resides in the PostgreSQL database.
    /// If not explicitly set, the default schema name configured in the
    /// <see cref="PostgresMessagingGatewayConnection"/> will be used.
    /// </summary>
    public string? SchemaName { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the queue store table in the PostgreSQL database.
    /// If not explicitly set, the default queue store table name configured in the
    /// <see cref="PostgresMessagingGatewayConnection"/> will be used.
    /// </summary>
    public string? QueueStoreTable { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether the message payload should be stored as binary JSON (JSONB)
    /// in the PostgreSQL database. Using JSONB can offer performance benefits.
    /// If not explicitly set, the default setting configured in the
    /// <see cref="PostgresMessagingGatewayConnection"/> will be used.
    /// </summary>
    public bool? BinaryMessagePayload { get; set; }
}
