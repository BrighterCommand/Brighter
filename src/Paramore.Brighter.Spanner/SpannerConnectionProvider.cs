using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.Spanner;

/// <summary>
/// Provides <see cref="SpannerConnection"/> instances for connecting to a Google Cloud Spanner database.
/// This class inherits from <see cref="RelationalDbConnectionProvider"/>, offering both
/// synchronous and asynchronous connection retrieval.
/// </summary>
/// <param name="configuration">The configuration containing the connection string for the Spanner database.</param>
public class SpannerConnectionProvider(IAmARelationalDatabaseConfiguration configuration)
    : RelationalDbConnectionProvider
{
    // Enable emulator detection so the connection routes to SPANNER_EMULATOR_HOST when it is set,
    // and behaves as normal production otherwise. The keyword serialises into the connection string.
    private readonly string _connectionString =
        new SpannerConnectionStringBuilder(configuration.ConnectionString)
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.ConnectionString;

    /// <inheritdoc />
    public override DbConnection GetConnection()
    {
        var connection = new SpannerConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <inheritdoc />
    public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SpannerConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
