#region Licence
/* The MIT License (MIT)

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

using System.Linq;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

/// <summary>
/// A <c>Publication</c> with no <c>Topic</c> compiles and configures, and then throws from
/// <c>Create()</c> — which is the second of the two defects that made the MSSQL samples
/// unstartable, and the only one that was not pinned.
/// </summary>
/// <remarks>
/// No database: <c>Create</c> validates the publication and constructs producers without opening
/// a connection.
/// </remarks>
[Trait("Category", "MSSQL")]
public class MsSqlProducerFactoryPublicationTopicTests
{
    private readonly RelationalDatabaseConfiguration _configuration =
        new("Server=localhost;Database=test;Trusted_Connection=True;", queueStoreTable: "QueueData");

    [Fact]
    public void When_the_publication_has_no_topic_should_throw()
    {
        // Arrange
        var factory = new MsSqlMessageProducerFactory(
            _configuration,
            [new Publication { RequestType = typeof(MyEvent) }]);

        // Act
        var exception = Assert.Throws<ConfigurationException>(() => factory.Create());

        // Assert
        Assert.Contains("Topic is missing from the publication", exception.Message);
    }

    // The control: without it the test above would pass against a factory that threw for every
    // publication, which would say nothing about the missing topic.
    [Fact]
    public void When_the_publication_has_a_topic_should_create_a_producer()
    {
        // Arrange
        var factory = new MsSqlMessageProducerFactory(
            _configuration,
            [new Publication { Topic = new RoutingKey("test.topic"), RequestType = typeof(MyEvent) }]);

        // Act
        var producers = factory.Create();

        // Assert
        Assert.Single(producers);
        Assert.Equal(new RoutingKey("test.topic"), producers.Keys.Single().RoutingKey);
    }
}
