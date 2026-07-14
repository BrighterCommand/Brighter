using System.Data;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Spanner;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Spanner.Connection;

[Trait("Category", "Spanner")]
public class SpannerConnectionProviderEmulatorTests
{
    // A bare connection string with no EmulatorDetection keyword — the shape production code
    // receives via RelationalDatabaseConfiguration. Built inline (not via Const) so this test
    // stays scoped to the provider's own emulator handling even after Const is patched.
    private readonly string _bareConnectionString =
        $"Data Source=projects/{GatewayFactory.GetProjectId()}/instances/brighter-spanner/databases/brightertests";

    [Fact]
    public async Task When_a_spanner_connection_provider_opens_a_connection_it_should_honour_the_spanner_emulator()
    {
        // Arrange
        var configuration = new RelationalDatabaseConfiguration(_bareConnectionString);
        var provider = new SpannerConnectionProvider(configuration);

        // Act
        await using var connection = await provider.GetConnectionAsync();

        using var command = ((SpannerConnection)connection).CreateSelectCommand("SELECT 1");
        var result = (long)(await command.ExecuteScalarAsync())!;

        // Assert
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal(1L, result);
    }
}
