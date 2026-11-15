using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.CircuitBreaker;

public class MsSqlTrippedTopicStoreSharedConnectionTests
{
    [Fact]
    public void When_the_mssql_trip_store_is_given_a_shared_connection_provider_should_throw()
    {
        // Arrange
        var sharedProvider = new MsSqlTransactionProvider(new RelationalDatabaseConfiguration(Configuration.DefaultConnectingString));

        // Act
        var thrown = Record.Exception(() => new MsSqlTrippedTopicStore(sharedProvider, "trips"));

        // Assert
        Assert.IsType<ConfigurationException>(thrown);
    }
}
