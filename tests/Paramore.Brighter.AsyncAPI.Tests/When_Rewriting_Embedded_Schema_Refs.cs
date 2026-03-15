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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Neuroglia.AsyncApi.v3;
using Xunit;

namespace Paramore.Brighter.AsyncAPI.Tests
{
    public class When_Rewriting_Embedded_Schema_Refs
    {
        private readonly AsyncApiOptions _options;

        public When_Rewriting_Embedded_Schema_Refs()
        {
            _options = new AsyncApiOptions
            {
                Title = "Test API",
                Version = "1.0.0",
                DisableAssemblyScanning = true
            };
        }

        private static IAmASchemaGenerator CreateSchemaGenerator(Type requestType, string schemaJson)
        {
            using var schemaDoc = JsonDocument.Parse(schemaJson);
            var schemaElement = schemaDoc.RootElement.Clone();

            var schemaGenerator = A.Fake<IAmASchemaGenerator>();
            A.CallTo(() => schemaGenerator.GenerateAsync(requestType, A<CancellationToken>.Ignored))
                .Returns(Task.FromResult<V3SchemaDefinition?>(new V3SchemaDefinition
                {
                    SchemaFormat = "application/schema+json;version=draft-07",
                    Schema = schemaElement
                }));

            return schemaGenerator;
        }

        [Fact]
        public async Task It_Should_Rewrite_Definitions_Refs_To_Resolve_Inside_Payload()
        {
            var schemaJson = """
            {
                "type": "object",
                "properties": {
                    "Address": { "$ref": "#/definitions/AddressInfo" }
                },
                "definitions": {
                    "AddressInfo": {
                        "type": "object",
                        "properties": {
                            "Street": { "type": "string" },
                            "City": { "type": "string" }
                        }
                    }
                }
            }
            """;

            var schemaGenerator = CreateSchemaGenerator(typeof(OrderWithAddress), schemaJson);

            var subscriptions = new[]
            {
                new Subscription(
                    new SubscriptionName("test"),
                    new ChannelName("test-channel"),
                    new RoutingKey("order.placed"),
                    requestType: typeof(OrderWithAddress),
                    messagePumpType: MessagePumpType.Reactor)
            };

            var generator = new AsyncApiDocumentGenerator(_options, schemaGenerator, subscriptions, null);
            var result = await generator.GenerateAsync();

            Assert.NotNull(result.Components?.Messages);
            var message = result.Components.Messages["OrderWithAddress"];
            Assert.NotNull(message.Payload);

            var payload = (JsonElement)message.Payload.Schema;
            var addressRef = payload.GetProperty("properties").GetProperty("Address").GetProperty("$ref").GetString();
            Assert.Equal("#/components/messages/OrderWithAddress/payload/definitions/AddressInfo", addressRef);
        }

        [Fact]
        public async Task It_Should_Rewrite_Defs_Refs_To_Resolve_Inside_Payload()
        {
            var schemaJson = """
            {
                "type": "object",
                "properties": {
                    "Contact": { "$ref": "#/$defs/ContactInfo" }
                },
                "$defs": {
                    "ContactInfo": {
                        "type": "object",
                        "properties": {
                            "Email": { "type": "string" }
                        }
                    }
                }
            }
            """;

            var schemaGenerator = CreateSchemaGenerator(typeof(OrderWithContact), schemaJson);

            var subscriptions = new[]
            {
                new Subscription(
                    new SubscriptionName("test"),
                    new ChannelName("test-channel"),
                    new RoutingKey("order.contact"),
                    requestType: typeof(OrderWithContact),
                    messagePumpType: MessagePumpType.Reactor)
            };

            var generator = new AsyncApiDocumentGenerator(_options, schemaGenerator, subscriptions, null);
            var result = await generator.GenerateAsync();

            Assert.NotNull(result.Components?.Messages);
            var message = result.Components.Messages["OrderWithContact"];
            Assert.NotNull(message.Payload);

            var payload = (JsonElement)message.Payload.Schema;
            var contactRef = payload.GetProperty("properties").GetProperty("Contact").GetProperty("$ref").GetString();
            Assert.Equal("#/components/messages/OrderWithContact/payload/$defs/ContactInfo", contactRef);
        }

        [Fact]
        public async Task It_Should_Not_Change_Payload_Without_Refs()
        {
            var schemaJson = """
            {
                "type": "object",
                "properties": {
                    "Name": { "type": "string" },
                    "Age": { "type": "integer" }
                }
            }
            """;

            var schemaGenerator = CreateSchemaGenerator(typeof(SimpleEvent), schemaJson);

            var subscriptions = new[]
            {
                new Subscription(
                    new SubscriptionName("test"),
                    new ChannelName("test-channel"),
                    new RoutingKey("simple.event"),
                    requestType: typeof(SimpleEvent),
                    messagePumpType: MessagePumpType.Reactor)
            };

            var generator = new AsyncApiDocumentGenerator(_options, schemaGenerator, subscriptions, null);
            var result = await generator.GenerateAsync();

            Assert.NotNull(result.Components?.Messages);
            var message = result.Components.Messages["SimpleEvent"];
            Assert.NotNull(message.Payload);

            var payload = (JsonElement)message.Payload.Schema;
            Assert.Equal("string", payload.GetProperty("properties").GetProperty("Name").GetProperty("type").GetString());
            Assert.Equal("integer", payload.GetProperty("properties").GetProperty("Age").GetProperty("type").GetString());
        }

        [Fact]
        public async Task It_Should_Rewrite_Deeply_Nested_Refs()
        {
            var schemaJson = """
            {
                "type": "object",
                "properties": {
                    "Billing": { "$ref": "#/definitions/BillingInfo" }
                },
                "definitions": {
                    "BillingInfo": {
                        "type": "object",
                        "properties": {
                            "Address": { "$ref": "#/definitions/AddressInfo" },
                            "Card": { "$ref": "#/definitions/CardInfo" }
                        }
                    },
                    "AddressInfo": {
                        "type": "object",
                        "properties": {
                            "Street": { "type": "string" }
                        }
                    },
                    "CardInfo": {
                        "type": "object",
                        "properties": {
                            "Number": { "type": "string" }
                        }
                    }
                }
            }
            """;

            var schemaGenerator = CreateSchemaGenerator(typeof(ComplexOrder), schemaJson);

            var subscriptions = new[]
            {
                new Subscription(
                    new SubscriptionName("test"),
                    new ChannelName("test-channel"),
                    new RoutingKey("order.complex"),
                    requestType: typeof(ComplexOrder),
                    messagePumpType: MessagePumpType.Reactor)
            };

            var generator = new AsyncApiDocumentGenerator(_options, schemaGenerator, subscriptions, null);
            var result = await generator.GenerateAsync();

            Assert.NotNull(result.Components?.Messages);
            var message = result.Components.Messages["ComplexOrder"];
            Assert.NotNull(message.Payload);

            var payload = (JsonElement)message.Payload.Schema;

            // Top-level property ref
            var billingRef = payload.GetProperty("properties").GetProperty("Billing").GetProperty("$ref").GetString();
            Assert.Equal("#/components/messages/ComplexOrder/payload/definitions/BillingInfo", billingRef);

            // Nested refs inside definitions
            var definitions = payload.GetProperty("definitions");
            var addressRef = definitions.GetProperty("BillingInfo")
                .GetProperty("properties").GetProperty("Address").GetProperty("$ref").GetString();
            Assert.Equal("#/components/messages/ComplexOrder/payload/definitions/AddressInfo", addressRef);

            var cardRef = definitions.GetProperty("BillingInfo")
                .GetProperty("properties").GetProperty("Card").GetProperty("$ref").GetString();
            Assert.Equal("#/components/messages/ComplexOrder/payload/definitions/CardInfo", cardRef);
        }

        [Fact]
        public async Task It_Should_Rewrite_Refs_In_Array_Items()
        {
            var schemaJson = """
            {
                "type": "object",
                "properties": {
                    "Items": {
                        "type": "array",
                        "items": { "$ref": "#/definitions/LineItem" }
                    }
                },
                "definitions": {
                    "LineItem": {
                        "type": "object",
                        "properties": {
                            "ProductName": { "type": "string" },
                            "Quantity": { "type": "integer" }
                        }
                    }
                }
            }
            """;

            var schemaGenerator = CreateSchemaGenerator(typeof(OrderWithItems), schemaJson);

            var publications = new[]
            {
                new Publication { Topic = new RoutingKey("order.items"), RequestType = typeof(OrderWithItems) }
            };

            var generator = new AsyncApiDocumentGenerator(_options, schemaGenerator, null, publications);
            var result = await generator.GenerateAsync();

            Assert.NotNull(result.Components?.Messages);
            var message = result.Components.Messages["OrderWithItems"];
            Assert.NotNull(message.Payload);

            var payload = (JsonElement)message.Payload.Schema;
            var itemsRef = payload.GetProperty("properties").GetProperty("Items")
                .GetProperty("items").GetProperty("$ref").GetString();
            Assert.Equal("#/components/messages/OrderWithItems/payload/definitions/LineItem", itemsRef);
        }

        [Fact]
        public async Task It_Should_Not_Rewrite_Non_Definition_Refs()
        {
            var schemaJson = """
            {
                "type": "object",
                "properties": {
                    "External": { "$ref": "https://example.com/schemas/external.json" }
                }
            }
            """;

            var schemaGenerator = CreateSchemaGenerator(typeof(ExternalRefEvent), schemaJson);

            var subscriptions = new[]
            {
                new Subscription(
                    new SubscriptionName("test"),
                    new ChannelName("test-channel"),
                    new RoutingKey("external.event"),
                    requestType: typeof(ExternalRefEvent),
                    messagePumpType: MessagePumpType.Reactor)
            };

            var generator = new AsyncApiDocumentGenerator(_options, schemaGenerator, subscriptions, null);
            var result = await generator.GenerateAsync();

            Assert.NotNull(result.Components?.Messages);
            var message = result.Components.Messages["ExternalRefEvent"];
            Assert.NotNull(message.Payload);

            var payload = (JsonElement)message.Payload.Schema;
            var externalRef = payload.GetProperty("properties").GetProperty("External").GetProperty("$ref").GetString();
            Assert.Equal("https://example.com/schemas/external.json", externalRef);
        }

        // Test types
        public class OrderWithAddress : Event
        {
            public OrderWithAddress() : base(Guid.NewGuid()) { }
        }

        public class OrderWithContact : Event
        {
            public OrderWithContact() : base(Guid.NewGuid()) { }
        }

        public class SimpleEvent : Event
        {
            public SimpleEvent() : base(Guid.NewGuid()) { }
        }

        public class ComplexOrder : Event
        {
            public ComplexOrder() : base(Guid.NewGuid()) { }
        }

        public class OrderWithItems : Event
        {
            public OrderWithItems() : base(Guid.NewGuid()) { }
        }

        public class ExternalRefEvent : Event
        {
            public ExternalRefEvent() : base(Guid.NewGuid()) { }
        }
    }
}
