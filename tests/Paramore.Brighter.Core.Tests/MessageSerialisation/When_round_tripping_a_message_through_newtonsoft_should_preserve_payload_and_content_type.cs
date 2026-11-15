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
using System.Net.Mime;
using Newtonsoft.Json;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class MessageNewtonsoftPayloadRoundTripTests
{
    [Test]
    [Arguments(CharacterEncoding.UTF8)]
    [Arguments(CharacterEncoding.ASCII)]
    [Arguments(CharacterEncoding.Base64)]
    [Arguments(CharacterEncoding.Raw)]
    public async Task When_round_tripping_a_message_through_newtonsoft_should_preserve_payload_and_content_type(CharacterEncoding encoding)
    {
        // Arrange
        byte[] payload = [0, 1, 65, 127, 128, 255];
        var header = new MessageHeader(
            messageId: new Id("order-42"),
            topic: new RoutingKey("orders"),
            messageType: MessageType.MT_EVENT,
            contentType: new ContentType("application/octet-stream; profile=orders"));
        var body = new MessageBody(payload, new ContentType("application/octet-stream; profile=orders"), encoding);
        var message = new Message(header, body);

        // Act
        var json = JsonConvert.SerializeObject(message);
        Message? roundTripped = JsonConvert.DeserializeObject<Message>(json);

        // Assert
        await Assert.That(roundTripped).IsNotNull();
        await Assert.That(roundTripped.Body.Memory.ToArray()).IsEqualTo(payload);
        await Assert.That(roundTripped.Body.CharacterEncoding).IsEqualTo(encoding);
        await Assert.That(roundTripped.Body.Value).IsEqualTo(body.Value);
        await Assert.That(roundTripped.Header.MessageId).IsEqualTo(header.MessageId);
        await Assert.That(roundTripped.Header.Topic).IsEqualTo(header.Topic);
        await AssertContentType(header.ContentType, roundTripped.Header.ContentType);
        await Assert.That(roundTripped.Body.ContentType).IsNotNull();
        await AssertContentType(body.ContentType!, roundTripped.Body.ContentType);
        await Assert.That(roundTripped.Header.ContentType.Parameters["profile"]).IsEqualTo("orders");
        await Assert.That(roundTripped.Body.ContentType.Parameters["profile"]).IsEqualTo("orders");
    }

    private static async Task AssertContentType(ContentType expected, ContentType actual)
    {
        await Assert.That(actual.MediaType).IsEqualTo(expected.MediaType);
        await Assert.That(actual.CharSet).IsEqualTo(expected.CharSet);
        await Assert.That(actual.Boundary).IsEqualTo(expected.Boundary);
        await Assert.That(actual.Name).IsEqualTo(expected.Name);
        await Assert.That(actual.Parameters.Count).IsEqualTo(expected.Parameters.Count);
        foreach (DictionaryEntry parameter in expected.Parameters)
        {
            await Assert.That(actual.Parameters[(string)parameter.Key]).IsEqualTo(parameter.Value);
        }
    }
}
