#region Licence
/* The MIT License (MIT)
Copyright © 2026 Jonny Olliff-Lee <jonny.ollifflee@gmail.com>

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
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Neuroglia.AsyncApi.v3;

namespace Paramore.Brighter.AsyncAPI.Tests
{
    public class When_Deduplicating_Channels_And_Messages
    {
        private readonly IAmASchemaGenerator _schemaGenerator;
        private readonly AsyncApiOptions _options;

        public When_Deduplicating_Channels_And_Messages()
        {
            _schemaGenerator = A.Fake<IAmASchemaGenerator>();
            using var doc = JsonDocument.Parse("{\"type\":\"object\"}");
            A.CallTo(() => _schemaGenerator.GenerateAsync(A<Type?>.Ignored, A<CancellationToken>.Ignored))
                .Returns(Task.FromResult<V3SchemaDefinition?>(new V3SchemaDefinition
                {
                    SchemaFormat = "application/schema+json;version=draft-07",
                    Schema = doc.RootElement.Clone()
                }));

            _options = new AsyncApiOptions
            {
                Title = "Test API",
                Version = "1.0.0",
                DisableAssemblyScanning = true
            };
        }

        [Test]
        public async Task It_Should_Dedup_Duplicate_Subscriptions_On_The_Same_Topic()
        {
            // Two subscriptions for the same routing key collapse into one channel + one
            // receive operation. The first subscription wins; the duplicate is dropped
            // rather than emitted as a misleading "_2"-suffixed second operation.
            var subscriptions = new[]
            {
                new Subscription(
                    new SubscriptionName("sub1"),
                    new ChannelName("ch1"),
                    new RoutingKey("shared.topic"),
                    requestType: typeof(SharedEvent),
                    messagePumpType: MessagePumpType.Reactor),
                new Subscription(
                    new SubscriptionName("sub2"),
                    new ChannelName("ch2"),
                    new RoutingKey("shared.topic"),
                    requestType: typeof(SharedEvent),
                    messagePumpType: MessagePumpType.Reactor)
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, subscriptions, null);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Channels).IsNotNull();
            await Assert.That(result.Channels).HasSingleItem();
            await Assert.That(result.Channels.ContainsKey("shared_topic")).IsTrue();

            await Assert.That(result.Operations).IsNotNull();
            await Assert.That(result.Operations).HasSingleItem();
            await Assert.That(result.Operations.ContainsKey("receive_shared_topic")).IsTrue();

            // Only one message component
            await Assert.That(result.Components?.Messages).IsNotNull();
            await Assert.That(result.Components.Messages).HasSingleItem();
            await Assert.That(result.Components.Messages.ContainsKey("SharedEvent")).IsTrue();
        }

        [Test]
        public async Task It_Should_Produce_One_Channel_With_Receive_And_Send_For_Same_Topic()
        {
            var subscriptions = new[]
            {
                new Subscription(
                    new SubscriptionName("sub1"),
                    new ChannelName("ch1"),
                    new RoutingKey("shared.topic"),
                    requestType: typeof(SharedEvent),
                    messagePumpType: MessagePumpType.Reactor)
            };

            var publications = new[]
            {
                new Publication { Topic = new RoutingKey("shared.topic"), RequestType = typeof(SharedEvent) }
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, subscriptions, publications);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Channels).IsNotNull();
            await Assert.That(result.Channels).HasSingleItem();

            await Assert.That(result.Operations).IsNotNull();
            await Assert.That(result.Operations.Count).IsEqualTo(2);
            await Assert.That(result.Operations.ContainsKey("receive_shared_topic")).IsTrue();
            await Assert.That(result.Operations.ContainsKey("send_shared_topic")).IsTrue();
        }

        [Test]
        public async Task It_Should_Produce_One_Message_Component_For_Same_Type_Across_Multiple_Subscriptions()
        {
            var subscriptions = new[]
            {
                new Subscription(
                    new SubscriptionName("sub1"),
                    new ChannelName("ch1"),
                    new RoutingKey("topic.one"),
                    requestType: typeof(SharedEvent),
                    messagePumpType: MessagePumpType.Reactor),
                new Subscription(
                    new SubscriptionName("sub2"),
                    new ChannelName("ch2"),
                    new RoutingKey("topic.two"),
                    requestType: typeof(SharedEvent),
                    messagePumpType: MessagePumpType.Reactor)
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, subscriptions, null);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Components?.Messages).IsNotNull();
            await Assert.That(result.Components.Messages).HasSingleItem();
        }

        [Test]
        public async Task It_Should_Dedup_Producer_Registry_Over_Supplemental_Publications()
        {
            // Both producer registry and supplemental declare the same (topic, action). The
            // producer-registry entry comes first and wins; the supplemental duplicate is
            // dropped rather than emitted as a misleading "_2"-suffixed second operation.
            var producerPubs = new[]
            {
                new Publication { Topic = new RoutingKey("dedup.topic"), RequestType = typeof(SharedEvent) }
            };

            var supplementalPubs = new[]
            {
                new Publication { Topic = new RoutingKey("dedup.topic"), RequestType = typeof(SharedEvent) }
            };

            var allPubs = producerPubs.Concat(supplementalPubs).ToArray();

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, allPubs);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Operations).IsNotNull();
            await Assert.That(result.Operations).HasSingleItem();
            await Assert.That(result.Operations.ContainsKey("send_dedup_topic")).IsTrue();
        }

        public class SharedEvent : Event
        {
            public SharedEvent() : base(Guid.NewGuid()) { }
        }
    }
}