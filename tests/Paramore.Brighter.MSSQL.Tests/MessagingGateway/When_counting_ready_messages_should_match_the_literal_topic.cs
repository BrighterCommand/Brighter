#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

#nullable enable

using System;
using Paramore.Brighter.MessagingGateway.MsSql.SqlQueues;
using Paramore.Brighter.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

[Trait("Category", "MSSQL")]
public class MsSqlMessageQueueTopicMatchingTests : IDisposable
{
    private readonly RelationalDatabaseConfiguration _configuration;
    private readonly MsSqlMessageQueue<string> _queue;

    public MsSqlMessageQueueTopicMatchingTests()
    {
        var testHelper = new MsSqlTestHelper();
        testHelper.SetupQueueDb();
        _configuration = testHelper.QueueConfiguration;
        _queue = new MsSqlMessageQueue<string>(_configuration, new MsSqlConnectionProvider(_configuration));
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("customer's-orders")]
    [InlineData("orders' OR 1=1 --")]
    public void When_counting_ready_messages_should_match_the_literal_topic(string topic)
    {
        //Arrange
        var routingKey = new RoutingKey(topic);
        string missingTopic = topic + "-missing";
        _queue.Send("first", routingKey);
        _queue.Send("second", routingKey);
        _queue.Send("other", new RoutingKey("other-topic"));

        //Act
        int count = _queue.NumberOfMessageReady(topic);
        int missingCount = _queue.NumberOfMessageReady(missingTopic);
        bool isReady = _queue.IsMessageReady(topic);
        bool isMissingReady = _queue.IsMessageReady(missingTopic);

        //Assert
        Assert.Equal(2, count);
        Assert.Equal(0, missingCount);
        Assert.True(isReady);
        Assert.False(isMissingReady);
    }

    public void Dispose() => Configuration.DeleteTable(_configuration.ConnectionString, $"[{_configuration.QueueStoreTable}]");
}
