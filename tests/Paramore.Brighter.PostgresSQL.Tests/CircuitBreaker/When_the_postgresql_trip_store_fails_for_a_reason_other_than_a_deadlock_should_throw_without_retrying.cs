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
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public class PostgreSqlTrippedTopicStoreOtherFailureTests : PostgreSqlTrippedTopicStoreTestBase
{
    [Fact]
    public async Task When_the_postgresql_trip_store_fails_for_a_reason_other_than_a_deadlock_should_throw_without_retrying()
    {
        // Arrange
        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new PostgreSqlTrippedTopicStore(recordingProvider, $"missing_{Guid.NewGuid():N}");

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => store.WriteAsync([new TopicTrip(new RoutingKey("orders"), DateTimeOffset.UtcNow)]));

        // Assert
        Assert.NotEqual("40P01", exception.SqlState);
        Assert.Single(recordingProvider.ExecutedCommands);
    }
}
