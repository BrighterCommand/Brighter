using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreSharedConnectionTests
{
    [Fact]
    public void When_the_mysql_trip_store_is_given_a_shared_connection_provider_should_throw()
    {
        // Arrange
        var sharedProvider = new MySqlTransactionProvider(new RelationalDatabaseConfiguration(Const.DefaultConnectingString));

        // Act
        var thrown = Record.Exception(() => new MySqlTrippedTopicStore(sharedProvider, "trips"));

        // Assert
        Assert.IsType<ConfigurationException>(thrown);
    }
}
