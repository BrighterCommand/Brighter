#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
    public class When_Generating_Document_Multiple_Times
    {
        private readonly IAmASchemaGenerator _schemaGenerator;
        private readonly AsyncApiOptions _options;

        public When_Generating_Document_Multiple_Times()
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
        public async Task It_Should_Produce_Identical_Documents_On_Repeated_Calls()
        {
            var subscriptions = new[]
            {
                new Subscription(
                    new SubscriptionName("sub1"),
                    new ChannelName("ch1"),
                    new RoutingKey("orders.created"),
                    requestType: typeof(TestEvent),
                    messagePumpType: MessagePumpType.Reactor)
            };

            var publications = new[]
            {
                new Publication { Topic = new RoutingKey("orders.shipped"), RequestType = typeof(TestEvent) }
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, subscriptions, publications);

            var first = await generator.GenerateAsync();
            var second = await generator.GenerateAsync();

            // Same channel count
            Assert.NotNull(first.Channels);
            Assert.NotNull(second.Channels);
            Assert.Equal(first.Channels.Count, second.Channels.Count);

            // Same operation count
            Assert.NotNull(first.Operations);
            Assert.NotNull(second.Operations);
            Assert.Equal(first.Operations.Count, second.Operations.Count);

            // Same operation keys
            Assert.Equal(
                first.Operations.Keys.OrderBy(k => k).ToList(),
                second.Operations.Keys.OrderBy(k => k).ToList());

            // Same message count
            Assert.NotNull(first.Components?.Messages);
            Assert.NotNull(second.Components?.Messages);
            Assert.Equal(first.Components.Messages.Count, second.Components.Messages.Count);
        }

        [Fact]
        public async Task It_Should_Not_Produce_Suffixed_Operation_Ids_On_Second_Call()
        {
            var subscriptions = new[]
            {
                new Subscription(
                    new SubscriptionName("sub1"),
                    new ChannelName("ch1"),
                    new RoutingKey("events.topic"),
                    requestType: typeof(TestEvent),
                    messagePumpType: MessagePumpType.Reactor)
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, subscriptions, null);

            await generator.GenerateAsync();
            var second = await generator.GenerateAsync();

            Assert.NotNull(second.Operations);
            Assert.Single(second.Operations);
            Assert.True(second.Operations.ContainsKey("receive_events_topic"));
            // The _2 suffixed key must NOT appear
            Assert.False(second.Operations.ContainsKey("receive_events_topic_2"));
        }

        public class TestEvent : Event
        {
            public TestEvent() : base(Guid.NewGuid()) { }
        }
    }
}
