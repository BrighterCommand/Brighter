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
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class MessageHeaderNewtonsoftContentTypeRoundTripTests
{
    [Test]
    [Arguments(null)]
    [Arguments("application/json")]
    [Arguments("application/json; charset=utf-8; profile=\"urn:example:orders\"")]
    [Arguments("multipart/mixed; boundary=orders-boundary; name=orders.json")]
    public async Task When_round_tripping_a_message_header_through_newtonsoft_should_preserve_content_type(string? mimeType)
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
        await Assert.That(roundTripped).IsNotNull();
        await Assert.That(roundTripped.MessageId).IsEqualTo(header.MessageId);
        await Assert.That(roundTripped.Topic).IsEqualTo(header.Topic);
        await Assert.That(roundTripped.ReplyTo).IsEqualTo(header.ReplyTo);
        await Assert.That(roundTripped.TraceParent).IsEqualTo(header.TraceParent);
        await Assert.That(roundTripped.TraceState).IsEqualTo(header.TraceState);
        await Assert.That(roundTripped.Baggage.Single().Value).IsEqualTo("acme");
        await Assert.That(roundTripped.Baggage.Single().Key).IsEqualTo("tenant");
        await Assert.That(roundTripped.Bag["SessionId"]).IsEqualTo("session-42");
        await Assert.That(roundTripped.ContentType.MediaType).IsEqualTo(header.ContentType.MediaType);
        await Assert.That(roundTripped.ContentType.CharSet).IsEqualTo(header.ContentType.CharSet);
        await Assert.That(roundTripped.ContentType.Boundary).IsEqualTo(header.ContentType.Boundary);
        await Assert.That(roundTripped.ContentType.Name).IsEqualTo(header.ContentType.Name);
        await Assert.That(roundTripped.ContentType.Parameters.Count).IsEqualTo(header.ContentType.Parameters.Count);

        JObject persistedContentType = await Assert.That(JObject.Parse(json)["ContentType"]).IsTypeOf<JObject>();
        await Assert.That(persistedContentType["MediaType"]!.Value<string>()).IsEqualTo(header.ContentType.MediaType);
        JArray persistedParameters = await Assert.That(persistedContentType["Parameters"]).IsTypeOf<JArray>();
        await Assert.That(persistedParameters.Count).IsEqualTo(header.ContentType.Parameters.Count);
        foreach (DictionaryEntry parameter in header.ContentType.Parameters)
        {
            var key = (string)parameter.Key;
            await Assert.That(roundTripped.ContentType.Parameters[key]).IsEqualTo(parameter.Value);
            await Assert.That(System.Linq.Enumerable.Any(persistedParameters, entry =>
                entry["Key"]!.Value<string>() == key && entry["Value"]!.Value<string>() == (string?)parameter.Value)).IsTrue();
        }
    }
}
