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

using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

public class GcpPublisherConfigurationCompositionTests
{
    [Fact]
    public async Task When_a_gcp_publication_sets_its_own_publisher_configuration_should_run_the_connection_configuration_first()
    {
        // Arrange
        var configurationsRun = new List<string>();

        var connection = new GcpMessagingGatewayConnection
        {
            ProjectId = "hook-composition-tests",
            PublisherConfiguration = _ => configurationsRun.Add("connection")
        };

        // Assume skips every server call while creating the producer; the insecure local endpoint lets the
        // client build without credentials, so no Pub/Sub server is needed.
        var publication = new GcpPublication
        {
            Topic = new RoutingKey("hook-composition-topic"),
            MakeChannels = OnMissingChannel.Assume,
            PublisherClientConfiguration = builder =>
            {
                configurationsRun.Add("publication");
                builder.Endpoint = "localhost:1";
                builder.ChannelCredentials = ChannelCredentials.Insecure;
            }
        };

        var factory = new GcpPubSubMessageProducerFactory(connection, [publication]);

        // Act
        var producers = await factory.CreateAsync();

        // Assert
        Assert.Equal(["connection", "publication"], configurationsRun);

        foreach (var producer in producers.Values)
        {
            await ((GcpMessageProducer)producer).DisposeAsync();
        }
    }
}
