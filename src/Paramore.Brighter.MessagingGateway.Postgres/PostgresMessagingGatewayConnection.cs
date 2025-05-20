namespace Paramore.Brighter.MessagingGateway.Postgres;

/// <summary>
/// Represents the configuration required to connect to a PostgreSQL database for Brighter's messaging gateway.
/// This class encapsulates the relational database configuration specific to PostgreSQL.
/// </summary>
public class PostgresMessagingGatewayConnection(RelationalDatabaseConfiguration configuration) : IAmGatewayConfiguration
{
    /// <summary>
    /// Gets the relational database configuration used for connecting to the PostgreSQL database.
    /// This configuration includes details such as the connection string, schema name, and queue store table name.
    /// </summary>
    public RelationalDatabaseConfiguration Configuration { get; } = configuration;
}
