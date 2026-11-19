#region Licence

/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

[Trait("Category", "Sqlite")]
public class TopicCountCancelledTokenTests
{
    private readonly CountingSqliteConnectionProvider _connectionProvider;
    private readonly IAmATopicCountingOutbox _outbox;

    public TopicCountCancelledTokenTests()
    {
        var configuration = new RelationalDatabaseConfiguration(
            $"Data Source={Uuid.New():N};Mode=Memory;Cache=Shared", outBoxTableName: "outbox");
        _connectionProvider = new CountingSqliteConnectionProvider(configuration);
        _outbox = new SqliteOutbox(configuration, _connectionProvider);
    }

    [Fact]
    public async Task When_counting_by_topic_with_a_cancelled_token_should_not_read_the_store()
    {
        //Arrange
        var ages = new Dictionary<RoutingKey, TimeSpan> { [new RoutingKey("orders")] = TimeSpan.FromMinutes(5) };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        //Act
        var exception = await Record.ExceptionAsync(() => _outbox.GetOutstandingMessageCountsByTopicAsync(
            ages, new RequestContext(), 100, cancellationToken: cancellation.Token));
        await Task.Delay(TimeSpan.FromSeconds(1));

        //Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.Equal(0, _connectionProvider.AsyncConnections);
    }
}
