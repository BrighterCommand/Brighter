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
    public class When_Generating_Document_From_Assembly_Scanning
    {
        private readonly IAmASchemaGenerator _schemaGenerator;

        public When_Generating_Document_From_Assembly_Scanning()
        {
            _schemaGenerator = A.Fake<IAmASchemaGenerator>();
            using var doc = JsonDocument.Parse("{\"type\":\"object\"}");
            A.CallTo(() => _schemaGenerator.GenerateAsync(A<Type?>.Ignored, A<CancellationToken>.Ignored))
                .Returns(Task.FromResult<V3SchemaDefinition?>(new V3SchemaDefinition
                {
                    SchemaFormat = "application/schema+json;version=draft-07",
                    Schema = doc.RootElement.Clone()
                }));
        }

        [Fact]
        public async Task It_Should_Discover_Types_With_PublicationTopic_Attribute()
        {
            var options = new AsyncApiOptions
            {
                Title = "Test API",
                Version = "1.0.0",
                AssembliesToScan = new[] { typeof(ScannableEvent).Assembly }
            };

            var generator = new AsyncApiDocumentGenerator(options, _schemaGenerator, null, null);
            var result = await generator.GenerateAsync();

            Assert.NotNull(result.Channels);
            Assert.True(result.Channels.ContainsKey("scannable_topic"));
            Assert.Equal("scannable.topic", result.Channels["scannable_topic"].Address);

            Assert.NotNull(result.Operations);
            Assert.True(result.Operations.ContainsKey("send_scannable_topic"));
            Assert.Equal(V3OperationAction.Send, result.Operations["send_scannable_topic"].Action);

            Assert.NotNull(result.Components?.Messages);
            Assert.True(result.Components.Messages.ContainsKey("ScannableEvent"));
        }

        [Fact]
        public async Task It_Should_Not_Scan_When_Disabled()
        {
            var options = new AsyncApiOptions
            {
                Title = "Test API",
                Version = "1.0.0",
                DisableAssemblyScanning = true,
                AssembliesToScan = new[] { typeof(ScannableEvent).Assembly }
            };

            var generator = new AsyncApiDocumentGenerator(options, _schemaGenerator, null, null);
            var result = await generator.GenerateAsync();

            Assert.Empty(result.Operations);
        }

        [Fact]
        public async Task It_Should_Let_DI_Source_Win_Dedup()
        {
            var options = new AsyncApiOptions
            {
                Title = "Test API",
                Version = "1.0.0",
                AssembliesToScan = new[] { typeof(ScannableEvent).Assembly }
            };

            var publications = new[]
            {
                new Publication
                {
                    Topic = new RoutingKey("scannable.topic"),
                    RequestType = typeof(ScannableEvent)
                }
            };

            var generator = new AsyncApiDocumentGenerator(options, _schemaGenerator, null, publications);
            var result = await generator.GenerateAsync();

            Assert.NotNull(result.Operations);
            // Should have exactly one send operation for this topic (from publications, not duplicated by scanning)
            Assert.True(result.Operations.ContainsKey("send_scannable_topic"));
            Assert.Single(result.Operations);
        }

        [Fact]
        public async Task It_Should_Skip_Assembly_Scanned_Type_When_DI_Has_Uniqueified_Operation_Id()
        {
            var options = new AsyncApiOptions
            {
                Title = "Test API",
                Version = "1.0.0",
                AssembliesToScan = new[] { typeof(ScannableEvent).Assembly }
            };

            // Two DI publications on the same topic as the [PublicationTopic]-decorated ScannableEvent.
            // The first gets send_scannable_topic, the second gets send_scannable_topic_2.
            // Assembly scanning must still detect that a send operation for scannable_topic
            // already exists from DI and skip the scanned type.
            var publications = new[]
            {
                new Publication
                {
                    Topic = new RoutingKey("scannable.topic"),
                    RequestType = typeof(ScannableEvent)
                },
                new Publication
                {
                    Topic = new RoutingKey("scannable.topic"),
                    RequestType = typeof(ScannableEvent)
                }
            };

            var generator = new AsyncApiDocumentGenerator(options, _schemaGenerator, null, publications);
            var result = await generator.GenerateAsync();

            Assert.NotNull(result.Operations);
            // Only the two DI publications should produce operations (send_scannable_topic and send_scannable_topic_2)
            // The assembly-scanned type must NOT add a third operation
            Assert.Equal(2, result.Operations.Count);
            Assert.True(result.Operations.ContainsKey("send_scannable_topic"));
            Assert.True(result.Operations.ContainsKey("send_scannable_topic_2"));
        }

        [PublicationTopic("scannable.topic")]
        public class ScannableEvent : Event
        {
            public ScannableEvent() : base(Guid.NewGuid()) { }
            public string Data { get; set; } = string.Empty;
        }
    }
}
