#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

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
