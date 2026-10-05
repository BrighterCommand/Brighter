#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia <irakli.gabisonia94@gmail.com>

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

#nullable enable

using System.Collections;
using System.Linq;
using System.Net.Mime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class MessageHeaderNewtonsoftContentTypeRoundTripTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("application/json")]
    [InlineData("application/json; charset=utf-8; profile=\"urn:example:orders\"")]
    [InlineData("multipart/mixed; boundary=orders-boundary; name=orders.json")]
    public void When_round_tripping_a_message_header_through_newtonsoft_should_preserve_content_type(string? mimeType)
    {
        // Arrange
        var baggage = new Baggage
        {
            { "tenant", "acme" }
        };
        var header = new MessageHeader(
            messageId: new Id("order-42"),
            topic: new RoutingKey("orders"),
            messageType: MessageType.MT_EVENT,
            replyTo: new RoutingKey("orders.reply"),
            contentType: mimeType == null ? null : new ContentType(mimeType),
            traceParent: new TraceParent("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"),
            traceState: new TraceState("vendor=value"),
            baggage: baggage);
        header.Bag["SessionId"] = "session-42";

        // Act
        var json = JsonConvert.SerializeObject(header);
        MessageHeader? roundTripped = JsonConvert.DeserializeObject<MessageHeader>(json);

        // Assert
        Assert.NotNull(roundTripped);
        Assert.Equal(header.MessageId, roundTripped.MessageId);
        Assert.Equal(header.Topic, roundTripped.Topic);
        Assert.Equal(header.ReplyTo, roundTripped.ReplyTo);
        Assert.Equal(header.TraceParent, roundTripped.TraceParent);
        Assert.Equal(header.TraceState, roundTripped.TraceState);
        Assert.Equal("acme", roundTripped.Baggage.Single().Value);
        Assert.Equal("tenant", roundTripped.Baggage.Single().Key);
        Assert.Equal("session-42", roundTripped.Bag["SessionId"]);
        Assert.Equal(header.ContentType.MediaType, roundTripped.ContentType.MediaType);
        Assert.Equal(header.ContentType.CharSet, roundTripped.ContentType.CharSet);
        Assert.Equal(header.ContentType.Boundary, roundTripped.ContentType.Boundary);
        Assert.Equal(header.ContentType.Name, roundTripped.ContentType.Name);
        Assert.Equal(header.ContentType.Parameters.Count, roundTripped.ContentType.Parameters.Count);

        JObject persistedContentType = Assert.IsType<JObject>(JObject.Parse(json)["ContentType"]);
        Assert.Equal(header.ContentType.MediaType, persistedContentType["MediaType"]!.Value<string>());
        JArray persistedParameters = Assert.IsType<JArray>(persistedContentType["Parameters"]);
        Assert.Equal(header.ContentType.Parameters.Count, persistedParameters.Count);
        foreach (DictionaryEntry parameter in header.ContentType.Parameters)
        {
            var key = (string)parameter.Key;
            Assert.Equal(parameter.Value, roundTripped.ContentType.Parameters[key]);
            Assert.Contains(persistedParameters, entry =>
                entry["Key"]!.Value<string>() == key && entry["Value"]!.Value<string>() == (string?)parameter.Value);
        }
    }
}
