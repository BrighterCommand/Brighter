using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace Paramore.Brighter.Oracle;

/// <summary>
/// A connection provider for Oracle using a connection string for database access.
/// </summary>
public class OracleConnectionProvider : RelationalDbConnectionProvider
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="OracleConnectionProvider"/> class.
    /// </summary>
    /// <param name="configuration">The relational database configuration containing the connection string.</param>
    /// <exception cref="ArgumentNullException">Thrown when the connection string is null or empty.</exception>
    public OracleConnectionProvider(IAmARelationalDatabaseConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.ConnectionString))
        {
            throw new ArgumentNullException(nameof(configuration.ConnectionString));
        }
        _connectionString = configuration.ConnectionString;
    }

    /// <summary>
    /// Creates and opens a new Oracle connection.
    /// This is not a shared connection; you should manage its lifetime.
    /// </summary>
    /// <returns>An open <see cref="DbConnection"/>.</returns>
    public override DbConnection GetConnection()
    {
        var connection = new OracleConnection(_connectionString);
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        return connection;
    }

    /// <summary>
    /// Creates and opens a new Oracle connection asynchronously.
    /// This is not a shared connection; you should manage its lifetime.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An open <see cref="DbConnection"/>.</returns>
    public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new OracleConnection(_connectionString);
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }
}
