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

namespace Paramore.Brighter.AsyncAPI.Tests
{
    public class When_Generating_Document_From_Publications
    {
        private readonly IAmASchemaGenerator _schemaGenerator;
        private readonly AsyncApiOptions _options;

        public When_Generating_Document_From_Publications()
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
        public async Task It_Should_Generate_Send_Operation_For_Publication_With_RequestType()
        {
            var publications = new[]
            {
                new Publication { Topic = new RoutingKey("order.created"), RequestType = typeof(TestOrderEvent) }
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, publications);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Channels).IsNotNull();
            await Assert.That(result.Channels.ContainsKey("order_created")).IsTrue();
            await Assert.That(result.Channels["order_created"].Address).IsEqualTo("order.created");

            await Assert.That(result.Operations).IsNotNull();
            await Assert.That(result.Operations.ContainsKey("send_order_created")).IsTrue();
            await Assert.That(result.Operations["send_order_created"].Action).IsEqualTo(V3OperationAction.Send);

            await Assert.That(result.Components?.Messages).IsNotNull();
            await Assert.That(result.Components.Messages.ContainsKey("TestOrderEvent")).IsTrue();
            await Assert.That(result.Components.Messages["TestOrderEvent"].Name).IsEqualTo("TestOrderEvent");
        }

        [Test]
        public async Task It_Should_Generate_Placeholder_Message_When_RequestType_Is_Null()
        {
            var publications = new[]
            {
                new Publication { Topic = new RoutingKey("order.created"), RequestType = null }
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, publications);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Components?.Messages).IsNotNull();
            await Assert.That(result.Components.Messages.ContainsKey("order_createdMessage")).IsTrue();
        }

        [Test]
        public async Task It_Should_Skip_Publication_With_Null_Topic()
        {
            var publications = new[]
            {
                new Publication { Topic = null, RequestType = typeof(TestOrderEvent) }
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, publications);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Channels).IsEmpty();
            await Assert.That(result.Operations).IsEmpty();
        }

        [Test]
        public async Task It_Should_Skip_Publication_With_Empty_Topic()
        {
            var publications = new[]
            {
                new Publication { Topic = new RoutingKey(""), RequestType = typeof(TestOrderEvent) }
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, publications);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Channels).IsEmpty();
            await Assert.That(result.Operations).IsEmpty();
        }

        [Test]
        public async Task It_Should_Handle_Null_Publications()
        {
            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, null);
            var result = await generator.GenerateAsync();

            await Assert.That(result.Channels).IsEmpty();
            await Assert.That(result.Operations).IsEmpty();
        }

        [Test]
        public async Task It_Should_Rewrite_Embedded_Schema_Refs_To_Message_Payload_Path()
        {
            using var schema = JsonDocument.Parse("""
                {
                  "definitions": {
                    "Event": {
                      "type": "object"
                    }
                  },
                  "allOf": [
                    {
                      "$ref": "#/definitions/Event"
                    }
                  ]
                }
                """);
            A.CallTo(() => _schemaGenerator.GenerateAsync(A<Type?>.Ignored, A<CancellationToken>.Ignored))
                .Returns(Task.FromResult<V3SchemaDefinition?>(new V3SchemaDefinition
                {
                    SchemaFormat = "application/schema+json;version=draft-07",
                    Schema = schema.RootElement.Clone()
                }));

            var publications = new[]
            {
                new Publication { Topic = new RoutingKey("order.created"), RequestType = typeof(TestOrderEvent) }
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, publications);
            var result = await generator.GenerateAsync();

            var payload = (JsonElement)result.Components!.Messages!["TestOrderEvent"].Payload!.Schema;
            var rewrittenRef = payload.GetProperty("allOf")[0].GetProperty("$ref").GetString();
            await Assert.That(rewrittenRef).IsEqualTo("#/components/schemas/Event");

            await Assert.That(payload.TryGetProperty("definitions", out _)).IsFalse();
            await Assert.That(result.Components.Schemas).IsNotNull();
            await Assert.That(result.Components.Schemas!.ContainsKey("Event")).IsTrue();
        }

        public class TestOrderEvent : Event
        {
            public TestOrderEvent() : base(Guid.NewGuid()) { }
            public string OrderId { get; set; } = string.Empty;
        }
    }
}