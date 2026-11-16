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
