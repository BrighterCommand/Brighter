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
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

[Trait("Category", "Sqlite")]
public class TopicCountValidationTests
{
    private readonly CountingSqliteConnectionProvider _connectionProvider;
    private readonly IAmATopicCountingOutbox _outbox;

    public TopicCountValidationTests()
    {
        var configuration = new RelationalDatabaseConfiguration(
            $"Data Source={Uuid.New():N};Mode=Memory;Cache=Shared", outBoxTableName: "outbox");
        _connectionProvider = new CountingSqliteConnectionProvider(configuration);
        _outbox = new SqliteOutbox(configuration, _connectionProvider);
    }

    [Theory]
    [InlineData("null-map", typeof(ArgumentNullException), "dispatchedSinceByTopic")]
    [InlineData("empty-topic", typeof(ArgumentException), "dispatchedSinceByTopic")]
    [InlineData("zero-age", typeof(ArgumentException), "dispatchedSinceByTopic")]
    [InlineData("negative-age", typeof(ArgumentException), "dispatchedSinceByTopic")]
    [InlineData("too-old", typeof(ArgumentException), "dispatchedSinceByTopic")]
    [InlineData("zero-ceiling", typeof(ArgumentOutOfRangeException), "maxCountPerTopic")]
    [InlineData("negative-ceiling", typeof(ArgumentOutOfRangeException), "maxCountPerTopic")]
    public async Task When_topic_count_arguments_are_invalid_should_reject_before_reading_the_store(
        string invalidArgument, Type exceptionType, string parameterName)
    {
        //Arrange
        var ages = new Dictionary<RoutingKey, TimeSpan> { [new RoutingKey("first-valid-topic")] = TimeSpan.FromMinutes(5) };
        var maxCountPerTopic = 100;
        switch (invalidArgument)
        {
            case "empty-topic":
                ages[RoutingKey.Empty] = TimeSpan.FromMinutes(5);
                break;
            case "zero-age":
                ages[new RoutingKey("invalid")] = TimeSpan.Zero;
                break;
            case "negative-age":
                ages[new RoutingKey("invalid")] = TimeSpan.FromTicks(-1);
                break;
            case "too-old":
                ages[new RoutingKey("invalid")] = TimeSpan.FromDays(365).Add(TimeSpan.FromTicks(1));
                break;
            case "zero-ceiling":
                maxCountPerTopic = 0;
                break;
            case "negative-ceiling":
                maxCountPerTopic = -1;
                break;
        }

        //Act
        var exception = await Assert.ThrowsAsync(exceptionType, () => _outbox.GetOutstandingMessageCountsByTopicAsync(
            invalidArgument == "null-map" ? null! : ages, new RequestContext(), maxCountPerTopic));

        //Assert
        Assert.Equal(parameterName, ((ArgumentException)exception).ParamName);
        Assert.Equal(0, _connectionProvider.AsyncConnections);
    }
}
