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
        /// Box-provisioning enforces a strict regex on identifier inputs (table, column, and
        /// schema names) at the framework chokepoint via <c>Paramore.Brighter.BoxProvisioning.Identifiers.AssertSafe</c>:
        /// the accepted character class is <c>[A-Za-z][A-Za-z0-9_]*</c> with length ≤ 64, no
        /// leading underscore, and no reserved-prefix collisions. The rule is intentionally
        /// over-restrictive — it applies the strictest-backend rule (Spanner's reserved
        /// <c>_</c> prefix) uniformly so identifier validation is platform-portable. Schema
        /// names that fail the regex will surface as <see cref="ConfigurationException"/>
        /// at provisioning entry rather than as a downstream SQL error. Per PR #4039
        /// reviewer item M2-7.
        /// </remarks>
        string? SchemaName { get; }
    }
}
