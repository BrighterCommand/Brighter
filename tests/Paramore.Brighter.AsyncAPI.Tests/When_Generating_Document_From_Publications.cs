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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Xunit;

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
                .Returns(Task.FromResult<JsonElement?>(doc.RootElement.Clone()));

            _options = new AsyncApiOptions
            {
                Title = "Test API",
                Version = "1.0.0",
                DisableAssemblyScanning = true
            };
        }

        [Fact]
        public async Task It_Should_Generate_Send_Operation_For_Publication_With_RequestType()
        {
            var publications = new[]
            {
                new Publication { Topic = new RoutingKey("order.created"), RequestType = typeof(TestOrderEvent) }
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, publications);
            var result = await generator.GenerateAsync();

            Assert.NotNull(result.Channels);
            Assert.True(result.Channels.ContainsKey("order_created"));
            Assert.Equal("order.created", result.Channels["order_created"].Address);

            Assert.NotNull(result.Operations);
            Assert.True(result.Operations.ContainsKey("send_order_created"));
            Assert.Equal("send", result.Operations["send_order_created"].Action);

            Assert.NotNull(result.Components?.Messages);
            Assert.True(result.Components.Messages.ContainsKey("TestOrderEvent"));
            Assert.Equal("TestOrderEvent", result.Components.Messages["TestOrderEvent"].Name);
        }

        [Fact]
        public async Task It_Should_Generate_Placeholder_Message_When_RequestType_Is_Null()
        {
            var publications = new[]
            {
                new Publication { Topic = new RoutingKey("order.created"), RequestType = null }
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, publications);
            var result = await generator.GenerateAsync();

            Assert.NotNull(result.Components?.Messages);
            Assert.True(result.Components.Messages.ContainsKey("order_createdMessage"));
        }

        [Fact]
        public async Task It_Should_Skip_Publication_With_Null_Topic()
        {
            var publications = new[]
            {
                new Publication { Topic = null, RequestType = typeof(TestOrderEvent) }
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, publications);
            var result = await generator.GenerateAsync();

            Assert.Null(result.Channels);
            Assert.Null(result.Operations);
        }

        [Fact]
        public async Task It_Should_Skip_Publication_With_Empty_Topic()
        {
            var publications = new[]
            {
                new Publication { Topic = new RoutingKey(""), RequestType = typeof(TestOrderEvent) }
            };

            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, publications);
            var result = await generator.GenerateAsync();

            Assert.Null(result.Channels);
            Assert.Null(result.Operations);
        }

        [Fact]
        public async Task It_Should_Handle_Null_Publications()
        {
            var generator = new AsyncApiDocumentGenerator(_options, _schemaGenerator, null, null);
            var result = await generator.GenerateAsync();

            Assert.Null(result.Channels);
            Assert.Null(result.Operations);
        }

        public class TestOrderEvent : Event
        {
            public TestOrderEvent() : base(Guid.NewGuid()) { }
            public string OrderId { get; set; } = string.Empty;
        }
    }
}
