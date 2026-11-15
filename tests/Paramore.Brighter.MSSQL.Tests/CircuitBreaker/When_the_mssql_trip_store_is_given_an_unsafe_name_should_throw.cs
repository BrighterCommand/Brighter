using System;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.CircuitBreaker;

public class MsSqlTrippedTopicStoreIdentifierTests
{
    [Theory]
    [InlineData("trips; DROP TABLE orders")]
    [InlineData("trips]")]
    [InlineData("trips`")]
    [InlineData("trips\"")]
    [InlineData("1trips")]
    [InlineData("_trips")]
    [InlineData("tr ips")]
    public void When_the_mssql_trip_store_is_given_an_unsafe_table_name_should_throw(string tableName)
    {
        // Arrange
        var connectionProvider = new MsSqlConnectionProvider(new RelationalDatabaseConfiguration(Configuration.DefaultConnectingString));

        // Act
        var thrown = Record.Exception(() => new MsSqlTrippedTopicStore(connectionProvider, tableName));

        // Assert
        Assert.IsType<ConfigurationException>(thrown);
    }

    [Fact]
    public void When_the_mssql_trip_store_is_given_an_unsafe_schema_name_should_throw()
    {
        // Arrange
        var connectionProvider = new MsSqlConnectionProvider(new RelationalDatabaseConfiguration(Configuration.DefaultConnectingString));

        // Act
        var thrown = Record.Exception(() => new MsSqlTrippedTopicStore(connectionProvider, "trips", "billing; DROP TABLE orders"));

        // Assert
        Assert.IsType<ConfigurationException>(thrown);
    }

    [Fact]
    public void When_the_mssql_ddl_is_requested_for_an_unsafe_name_should_throw()
    {
        // Act
        var unsafeTable = Record.Exception(() => MsSqlTrippedTopicStore.GetDDL("trips; DROP TABLE orders"));
        var unsafeSchema = Record.Exception(() => MsSqlTrippedTopicStore.GetDDL("trips", "billing; DROP TABLE orders"));

        // Assert
        Assert.IsType<ConfigurationException>(unsafeTable);
        Assert.IsType<ConfigurationException>(unsafeSchema);
    }

    [Fact]
    public void When_the_mssql_purge_is_requested_for_an_unsafe_name_should_throw()
    {
        // Act
        var unsafeTable = Record.Exception(() => MsSqlTrippedTopicStore.GetPurgeSql("trips; DROP TABLE orders"));
        var unsafeSchema = Record.Exception(() => MsSqlTrippedTopicStore.GetPurgeSql("trips", "billing; DROP TABLE orders"));

        // Assert
        Assert.IsType<ConfigurationException>(unsafeTable);
        Assert.IsType<ConfigurationException>(unsafeSchema);
    }
}
