using System;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreIdentifierTests
{
    [Theory]
    [InlineData("trips; DROP TABLE orders")]
    [InlineData("trips]")]
    [InlineData("trips`")]
    [InlineData("trips\"")]
    [InlineData("1trips")]
    [InlineData("_trips")]
    [InlineData("tr ips")]
    public void When_the_mysql_trip_store_is_given_an_unsafe_table_name_should_throw(string tableName)
    {
        // Arrange
        var connectionProvider = new MySqlConnectionProvider(new RelationalDatabaseConfiguration(Const.DefaultConnectingString));

        // Act
        var thrown = Record.Exception(() => new MySqlTrippedTopicStore(connectionProvider, tableName));

        // Assert
        Assert.IsType<ConfigurationException>(thrown);
    }

    [Fact]
    public void When_the_mysql_trip_store_is_given_an_unsafe_schema_name_should_throw()
    {
        // Arrange
        var connectionProvider = new MySqlConnectionProvider(new RelationalDatabaseConfiguration(Const.DefaultConnectingString));

        // Act
        var thrown = Record.Exception(() => new MySqlTrippedTopicStore(connectionProvider, "trips", "billing; DROP TABLE orders"));

        // Assert
        Assert.IsType<ConfigurationException>(thrown);
    }

    [Fact]
    public void When_the_mysql_ddl_is_requested_for_an_unsafe_name_should_throw()
    {
        // Act
        var unsafeTable = Record.Exception(() => MySqlTrippedTopicStore.GetDDL("trips; DROP TABLE orders"));
        var unsafeSchema = Record.Exception(() => MySqlTrippedTopicStore.GetDDL("trips", "billing; DROP TABLE orders"));

        // Assert
        Assert.IsType<ConfigurationException>(unsafeTable);
        Assert.IsType<ConfigurationException>(unsafeSchema);
    }

    [Fact]
    public void When_the_mysql_purge_is_requested_for_an_unsafe_name_should_throw()
    {
        // Act
        var unsafeTable = Record.Exception(() => MySqlTrippedTopicStore.GetPurgeSql("trips; DROP TABLE orders"));
        var unsafeSchema = Record.Exception(() => MySqlTrippedTopicStore.GetPurgeSql("trips", "billing; DROP TABLE orders"));

        // Assert
        Assert.IsType<ConfigurationException>(unsafeTable);
        Assert.IsType<ConfigurationException>(unsafeSchema);
    }
}
