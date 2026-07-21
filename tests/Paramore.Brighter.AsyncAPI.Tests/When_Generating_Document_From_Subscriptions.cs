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
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Neuroglia.AsyncApi.v3;

namespace Paramore.Brighter.AsyncAPI.Tests
{
    public class When_Generating_Document_From_Subscriptions
    {
        private readonly IAmASchemaGenerator _schemaGenerator;
        private readonly AsyncApiOptions _options;

        public When_Generating_Document_From_Subscriptions()
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
        public async Task It_Should_Generate_Receive_Operation_For_Subscription_With_RequestType()
        {
            var subscriptions = new[]
            {
                new Subscription(
                    new SubscriptionName("test"),
                    new ChannelName("test-channel"),
                    new RoutingKey("order.created"),
                    requestType: typeof(TestEvent),
                    messagePumpType: MessagePumpType.Reactor)
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, subscriptions, null);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Channels).IsNotNull();
            await Assert.That(result.Channels.ContainsKey("order_created")).IsTrue();
            await Assert.That(result.Channels["order_created"].Address).IsEqualTo("order.created");

            await Assert.That(result.Operations).IsNotNull();
            await Assert.That(result.Operations.ContainsKey("receive_order_created")).IsTrue();
            await Assert.That(result.Operations["receive_order_created"].Action).IsEqualTo(V3OperationAction.Receive);

            await Assert.That(result.Components?.Messages).IsNotNull();
            await Assert.That(result.Components.Messages.ContainsKey("TestEvent")).IsTrue();
            await Assert.That(result.Components.Messages["TestEvent"].Name).IsEqualTo("TestEvent");
        }

        [Test]
        public async Task It_Should_Generate_Placeholder_Message_When_RequestType_Uses_MapRequestType()
        {
            var subscriptions = new[]
            {
                new Subscription(
                    new SubscriptionName("test"),
                    new ChannelName("test-channel"),
                    new RoutingKey("order.created"),
                    requestType: null,
                    getRequestType: _ => typeof(TestEvent),
                    messagePumpType: MessagePumpType.Reactor)
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, subscriptions, null);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Components?.Messages).IsNotNull();
            await Assert.That(result.Components.Messages.ContainsKey("order_createdMessage")).IsTrue();
        }

        [Test]
        public async Task It_Should_Skip_Subscription_With_Empty_RoutingKey()
        {
            var subscriptions = new[]
            {
                new Subscription(
                    new SubscriptionName("test"),
                    new ChannelName("test-channel"),
                    new RoutingKey(""),
                    requestType: typeof(TestEvent),
                    messagePumpType: MessagePumpType.Reactor)
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, subscriptions, null);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Channels).IsEmpty();
            await Assert.That(result.Operations).IsEmpty();
        }

        [Test]
        public async Task It_Should_Handle_Null_Subscriptions()
        {
            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, null);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Channels).IsEmpty();
            await Assert.That(result.Operations).IsEmpty();
        }

        [Test]
        public async Task It_Should_Include_Servers_When_Configured()
        {
            _options.Servers = new Dictionary<string, V3ServerDefinition>
            {
                ["production"] = new V3ServerDefinition
                {
                    Host = "rabbitmq:5672",
                    Protocol = "amqp"
                }
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, null);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Servers).IsNotNull();
            await Assert.That(result.Servers.ContainsKey("production")).IsTrue();
            await Assert.That(result.Servers["production"].Host).IsEqualTo("rabbitmq:5672");
        }

        [Test]
        public async Task It_Should_Isolate_Servers_From_Options_Mutations()
        {
            _options.Servers = new Dictionary<string, V3ServerDefinition>
            {
                ["production"] = new V3ServerDefinition
                {
                    Host = "rabbitmq:5672",
                    Protocol = "amqp"
                }
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, null);
            var result = await generator.GenerateAsync();

            // Mutate options after generation
            _options.Servers["staging"] = new V3ServerDefinition
            {
                Host = "staging-rabbitmq:5672",
                Protocol = "amqp"
            };

            // The previously generated document must NOT contain the new key
            await Assert.That(result.Servers).IsNotNull();
            await Assert.That(result.Servers).HasSingleItem();
            await Assert.That(result.Servers.ContainsKey("production")).IsTrue();
            await Assert.That(result.Servers.ContainsKey("staging")).IsFalse();
        }

        public class TestEvent : Event
        {
            public TestEvent() : base(Guid.NewGuid()) { }
            public string Name { get; set; } = string.Empty;
        }
    }
}