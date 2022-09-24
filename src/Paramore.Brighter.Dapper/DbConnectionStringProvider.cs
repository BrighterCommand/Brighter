namespace Paramore.Brighter.Dapper
{
    /// <summary>
    /// Dapper is a set of extension methods for a ADO.NET DbConnection. Usage of Dapper thus depends on a DbConnection
    /// To allow us to use that same connection, and any ambient transaction when writing to the Outbox, we need to provide that
    /// connection to Brighter code. We use a "unit of work" to wrap the connection, and provide a dependency for handlers etc.
    /// to ensure they use the shared connection. To allow the unit of work to be "scoped" or "transient" we need to provide the unit
    /// of work with the connection string. We wrap the connection string here, to make the dependency for the "unit of work" clear.
    /// </summary>
    public class DbConnectionStringProvider
    {
        public DbConnectionStringProvider(string dbConnectionString)
        {
            ConnectionString = dbConnectionString;
        }
        
        public string ConnectionString { get; }
    }
}
