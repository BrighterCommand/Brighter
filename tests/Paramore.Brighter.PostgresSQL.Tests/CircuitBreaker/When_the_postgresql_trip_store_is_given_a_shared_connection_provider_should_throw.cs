using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.PostgreSql;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public class PostgreSqlTrippedTopicStoreSharedConnectionTests
{
    [Fact]
    public void When_the_postgresql_trip_store_is_given_a_shared_connection_provider_should_throw()
    {
        // Arrange
        var sharedProvider = new PostgreSqlTransactionProvider(new RelationalDatabaseConfiguration(PostgreSqlSettings.TestsBrighterConnectionString));

        // Act
        var thrown = Record.Exception(() => new PostgreSqlTrippedTopicStore(sharedProvider, "trips"));

        // Assert
        Assert.IsType<ConfigurationException>(thrown);
    }
}
