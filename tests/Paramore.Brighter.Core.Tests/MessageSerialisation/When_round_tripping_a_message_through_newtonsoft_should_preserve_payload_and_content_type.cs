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
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class MessageNewtonsoftPayloadRoundTripTests
{
    [Theory]
    [InlineData(CharacterEncoding.UTF8)]
    [InlineData(CharacterEncoding.ASCII)]
    [InlineData(CharacterEncoding.Base64)]
    [InlineData(CharacterEncoding.Raw)]
    public void When_round_tripping_a_message_through_newtonsoft_should_preserve_payload_and_content_type(CharacterEncoding encoding)
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
        Assert.NotNull(roundTripped);
        Assert.Equal(payload, roundTripped.Body.Memory.ToArray());
        Assert.Equal(encoding, roundTripped.Body.CharacterEncoding);
        Assert.Equal(body.Value, roundTripped.Body.Value);
        Assert.Equal(header.MessageId, roundTripped.Header.MessageId);
        Assert.Equal(header.Topic, roundTripped.Header.Topic);
        AssertContentType(header.ContentType, roundTripped.Header.ContentType);
        Assert.NotNull(roundTripped.Body.ContentType);
        AssertContentType(body.ContentType!, roundTripped.Body.ContentType);
        Assert.Equal("orders", roundTripped.Header.ContentType.Parameters["profile"]);
        Assert.Equal("orders", roundTripped.Body.ContentType.Parameters["profile"]);
    }

    private static void AssertContentType(ContentType expected, ContentType actual)
    {
        Assert.Equal(expected.MediaType, actual.MediaType);
        Assert.Equal(expected.CharSet, actual.CharSet);
        Assert.Equal(expected.Boundary, actual.Boundary);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Parameters.Count, actual.Parameters.Count);
        foreach (DictionaryEntry parameter in expected.Parameters)
        {
            Assert.Equal(parameter.Value, actual.Parameters[(string)parameter.Key]);
        }
    }
}
