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
using Xunit;

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
                .Returns(Task.FromResult<JsonElement?>(doc.RootElement.Clone()));

            _options = new AsyncApiOptions
            {
                Title = "Test API",
                Version = "1.0.0",
                DisableAssemblyScanning = true
            };
        }

        [Fact]
        public async Task It_Should_Produce_One_Channel_And_Two_Operations_For_Duplicate_Subscriptions()
        {
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

            // One channel for the shared topic
            Assert.NotNull(result.Channels);
            Assert.Single(result.Channels);
            Assert.True(result.Channels.ContainsKey("shared_topic"));

            // Two receive operations with unique IDs
            Assert.NotNull(result.Operations);
            Assert.Equal(2, result.Operations.Count);
            Assert.True(result.Operations.ContainsKey("receive_shared_topic"));
            Assert.True(result.Operations.ContainsKey("receive_shared_topic_2"));

            // Only one message component
            Assert.NotNull(result.Components?.Messages);
            Assert.Single(result.Components.Messages);
            Assert.True(result.Components.Messages.ContainsKey("SharedEvent"));
        }

        [Fact]
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

            Assert.NotNull(result.Channels);
            Assert.Single(result.Channels);

            Assert.NotNull(result.Operations);
            Assert.Equal(2, result.Operations.Count);
            Assert.True(result.Operations.ContainsKey("receive_shared_topic"));
            Assert.True(result.Operations.ContainsKey("send_shared_topic"));
        }

        [Fact]
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

            Assert.NotNull(result.Components?.Messages);
            Assert.Single(result.Components.Messages);
        }

        [Fact]
        public async Task It_Should_Dedup_Producer_Registry_Over_Supplemental_Publications()
        {
            // Both producer registry and supplemental have the same topic
            var producerPubs = new[]
            {
                new Publication { Topic = new RoutingKey("dedup.topic"), RequestType = typeof(SharedEvent) }
            };

            var supplementalPubs = new[]
            {
                new Publication { Topic = new RoutingKey("dedup.topic"), RequestType = typeof(SharedEvent) }
            };

            // Combine: producer registry publications first, supplemental second
            var allPubs = producerPubs.Concat(supplementalPubs).ToArray();

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, allPubs);
            var result = await generator.GenerateAsync();

            Assert.NotNull(result.Operations);
            // Should have send_dedup_topic and send_dedup_topic_2 since both get added
            // The producer registry "wins" by being first; the supplemental gets a unique ID
            Assert.Equal(2, result.Operations.Count);
        }

        public class SharedEvent : Event
        {
            public SharedEvent() : base(Guid.NewGuid()) { }
        }
    }
}
