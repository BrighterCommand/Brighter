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

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Paramore.Brighter.AsyncAPI.Model;
using Xunit;

namespace Paramore.Brighter.AsyncAPI.Tests.Model
{
    public class When_Serializing_AsyncApi_Document
    {
        private static readonly JsonSerializerOptions s_options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        [Fact]
        public void It_Should_Produce_Correct_AsyncApi_Version()
        {
            var doc = new AsyncApiDocument();
            var json = JsonSerializer.Serialize(doc, s_options);
            using var parsed = JsonDocument.Parse(json);
            Assert.Equal("3.0.0", parsed.RootElement.GetProperty("asyncapi").GetString());
        }

        [Fact]
        public void It_Should_Serialize_Info_Section()
        {
            var doc = new AsyncApiDocument
            {
                Info = new AsyncApiInfo { Title = "My API", Version = "1.0.0", Description = "A test API" }
            };
            var json = JsonSerializer.Serialize(doc, s_options);
            using var parsed = JsonDocument.Parse(json);
            var info = parsed.RootElement.GetProperty("info");
            Assert.Equal("My API", info.GetProperty("title").GetString());
            Assert.Equal("1.0.0", info.GetProperty("version").GetString());
            Assert.Equal("A test API", info.GetProperty("description").GetString());
        }

        [Fact]
        public void It_Should_Omit_Null_Properties()
        {
            var doc = new AsyncApiDocument();
            var json = JsonSerializer.Serialize(doc, s_options);
            using var parsed = JsonDocument.Parse(json);
            Assert.False(parsed.RootElement.TryGetProperty("servers", out _));
            Assert.False(parsed.RootElement.TryGetProperty("channels", out _));
            Assert.False(parsed.RootElement.TryGetProperty("operations", out _));
            Assert.False(parsed.RootElement.TryGetProperty("components", out _));
        }

        [Fact]
        public void It_Should_Serialize_Ref_With_Dollar_Sign()
        {
            var refObj = new AsyncApiRef { Ref = "#/components/messages/MyMessage" };
            var json = JsonSerializer.Serialize(refObj, s_options);
            using var parsed = JsonDocument.Parse(json);
            Assert.Equal("#/components/messages/MyMessage", parsed.RootElement.GetProperty("$ref").GetString());
        }

        [Fact]
        public void It_Should_Round_Trip_Complete_Document()
        {
            var doc = new AsyncApiDocument
            {
                Info = new AsyncApiInfo { Title = "Test", Version = "2.0.0" },
                Channels = new Dictionary<string, AsyncApiChannel>
                {
                    ["myChannel"] = new AsyncApiChannel
                    {
                        Address = "my.topic",
                        Messages = new Dictionary<string, AsyncApiRef>
                        {
                            ["MyMessage"] = new AsyncApiRef { Ref = "#/components/messages/MyMessage" }
                        }
                    }
                },
                Operations = new Dictionary<string, AsyncApiOperation>
                {
                    ["receive_myChannel"] = new AsyncApiOperation
                    {
                        Action = "receive",
                        Channel = new AsyncApiRef { Ref = "#/channels/myChannel" },
                        Messages = new List<AsyncApiRef>
                        {
                            new AsyncApiRef { Ref = "#/channels/myChannel/messages/MyMessage" }
                        }
                    }
                },
                Components = new AsyncApiComponents
                {
                    Messages = new Dictionary<string, AsyncApiMessage>
                    {
                        ["MyMessage"] = new AsyncApiMessage
                        {
                            Name = "MyMessage",
                            ContentType = "application/json"
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(doc, s_options);
            var deserialized = JsonSerializer.Deserialize<AsyncApiDocument>(json, s_options);

            Assert.NotNull(deserialized);
            Assert.Equal("3.0.0", deserialized.AsyncApi);
            Assert.Equal("Test", deserialized.Info.Title);
            Assert.NotNull(deserialized.Channels);
            Assert.True(deserialized.Channels.ContainsKey("myChannel"));
            Assert.Equal("my.topic", deserialized.Channels["myChannel"].Address);
            Assert.NotNull(deserialized.Operations);
            Assert.True(deserialized.Operations.ContainsKey("receive_myChannel"));
            Assert.Equal("receive", deserialized.Operations["receive_myChannel"].Action);
        }

        [Fact]
        public void It_Should_Serialize_Servers()
        {
            var doc = new AsyncApiDocument
            {
                Servers = new Dictionary<string, AsyncApiServer>
                {
                    ["production"] = new AsyncApiServer
                    {
                        Host = "rabbitmq.example.com:5672",
                        Protocol = "amqp",
                        Description = "Production RabbitMQ"
                    }
                }
            };

            var json = JsonSerializer.Serialize(doc, s_options);
            using var parsed = JsonDocument.Parse(json);
            var server = parsed.RootElement.GetProperty("servers").GetProperty("production");
            Assert.Equal("rabbitmq.example.com:5672", server.GetProperty("host").GetString());
            Assert.Equal("amqp", server.GetProperty("protocol").GetString());
            Assert.Equal("Production RabbitMQ", server.GetProperty("description").GetString());
        }

        [Fact]
        public void It_Should_Serialize_Message_With_Payload()
        {
            using var payloadDoc = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{\"Name\":{\"type\":\"string\"}}}");
            var message = new AsyncApiMessage
            {
                Name = "TestMessage",
                ContentType = "application/json",
                Payload = payloadDoc.RootElement.Clone()
            };

            var json = JsonSerializer.Serialize(message, s_options);
            using var parsed = JsonDocument.Parse(json);
            Assert.Equal("TestMessage", parsed.RootElement.GetProperty("name").GetString());
            var payload = parsed.RootElement.GetProperty("payload");
            Assert.Equal("object", payload.GetProperty("type").GetString());
        }
    }
}
