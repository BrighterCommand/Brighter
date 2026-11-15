using System;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.PostgreSql;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public class PostgreSqlTrippedTopicStoreIdentifierTests
{
    [Theory]
    [InlineData("trips; DROP TABLE orders")]
    [InlineData("trips]")]
    [InlineData("trips`")]
    [InlineData("trips\"")]
    [InlineData("1trips")]
    [InlineData("_trips")]
    [InlineData("tr ips")]
    public void When_the_postgresql_trip_store_is_given_an_unsafe_table_name_should_throw(string tableName)
    {
        // Arrange
        var connectionProvider = new PostgreSqlConnectionProvider(new RelationalDatabaseConfiguration(PostgreSqlSettings.TestsBrighterConnectionString));

        // Act
        var thrown = Record.Exception(() => new PostgreSqlTrippedTopicStore(connectionProvider, tableName));

        // Assert
        Assert.IsType<ConfigurationException>(thrown);
    }

    [Fact]
    public void When_the_postgresql_trip_store_is_given_an_unsafe_schema_name_should_throw()
    {
        // Arrange
        var connectionProvider = new PostgreSqlConnectionProvider(new RelationalDatabaseConfiguration(PostgreSqlSettings.TestsBrighterConnectionString));

        // Act
        var thrown = Record.Exception(() => new PostgreSqlTrippedTopicStore(connectionProvider, "trips", "billing; DROP TABLE orders"));

        // Assert
        Assert.IsType<ConfigurationException>(thrown);
    }

    [Fact]
    public void When_the_postgresql_ddl_is_requested_for_an_unsafe_name_should_throw()
    {
        // Act
        var unsafeTable = Record.Exception(() => PostgreSqlTrippedTopicStore.GetDDL("trips; DROP TABLE orders"));
        var unsafeSchema = Record.Exception(() => PostgreSqlTrippedTopicStore.GetDDL("trips", "billing; DROP TABLE orders"));

        // Assert
        Assert.IsType<ConfigurationException>(unsafeTable);
        Assert.IsType<ConfigurationException>(unsafeSchema);
    }

    [Fact]
    public void When_the_postgresql_purge_is_requested_for_an_unsafe_name_should_throw()
    {
        // Act
        var unsafeTable = Record.Exception(() => PostgreSqlTrippedTopicStore.GetPurgeSql("trips; DROP TABLE orders"));
        var unsafeSchema = Record.Exception(() => PostgreSqlTrippedTopicStore.GetPurgeSql("trips", "billing; DROP TABLE orders"));

        // Assert
        Assert.IsType<ConfigurationException>(unsafeTable);
        Assert.IsType<ConfigurationException>(unsafeSchema);
    }
}
