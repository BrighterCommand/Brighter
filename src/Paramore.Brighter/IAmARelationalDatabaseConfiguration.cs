namespace Paramore.Brighter
{
    public interface IAmARelationalDatabaseConfiguration
    {
        /// <summary>
        /// Is the message payload binary, or a UTF-8 string. Default is false or UTF-8
        /// </summary>
        bool BinaryMessagePayload { get; }
        
        /// <summary>
        /// Whether to persist the message payload using the database’s native JSON type
        /// </summary>
        bool JsonMessagePayload { get; }

        /// <summary>
        /// Gets the connection string.
        /// </summary>
        /// <value>The connection string.</value>
        string ConnectionString { get; }

        /// <summary>
        /// Gets the name of the database containing the tables.
        /// </summary>
        /// <value>The name of the database.</value>
        string DatabaseName { get; }

        /// <summary>
        /// Gets the name of the inbox table.
        /// </summary>
        /// <value>The name of the inbox table.</value>
        string InBoxTableName { get; }

        /// <summary>
        /// Gets the name of the outbox table.
        /// </summary>
        /// <value>The name of the outbox table.</value>
        string OutBoxTableName { get; }

        /// <summary>
        /// Gets the name of the queue table.
        /// </summary>
        string QueueStoreTable { get; }

        /// <summary>
        /// Gets the name of the schema containing the tables.
        /// </summary>
        /// <value>The schema name, or <c>null</c> for the backend's default schema.</value>
        /// <remarks>
        /// Box-provisioning enforces a regex on identifier inputs (table, column, and schema
        /// names) at the framework chokepoint via
        /// <c>Paramore.Brighter.BoxProvisioning.Identifiers.AssertSafe</c>: the accepted pattern
        /// is <c>^[A-Za-z][A-Za-z0-9_]*$</c> (must start with an ASCII letter; remaining
        /// characters are ASCII letters, digits, or underscore). The first-character letter
        /// class excludes leading underscores — Spanner reserves <c>_</c>-prefixed names while
        /// other backends accept them, so the regex picks the strictest backend's rule to keep
        /// portable configuration safe. Schema names that fail the regex surface as
        /// <see cref="ConfigurationException"/> at provisioning entry rather than as a
        /// downstream SQL error. Per PR #4039 reviewer items M2-7 and the multi-part 2026-05-20
        /// review item 1.
        /// </remarks>
        string? SchemaName { get; }
    }
}
