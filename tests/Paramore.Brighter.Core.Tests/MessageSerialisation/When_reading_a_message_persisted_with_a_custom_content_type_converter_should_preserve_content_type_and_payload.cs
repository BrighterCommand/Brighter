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

using Newtonsoft.Json;
using Paramore.Brighter.Core.Tests.MessageSerialisation.TestDoubles;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class CustomContentTypePersistedMessageTests
{
    [Test]
    public async Task When_reading_a_message_persisted_with_a_custom_content_type_converter_should_preserve_content_type_and_payload()
    {
        // Arrange: a persisted document using the application's MIME string converter.
        const string JSON = """
            {
              "Header": {
                "MessageId": "order-42",
                "Topic": "orders",
                "MessageType": 2,
                "ContentType": "application/json; charset=utf-8; profile=orders"
              },
              "Body": {
                "Bytes": "aGVsbG8=",
                "ContentType": "application/json; charset=utf-8; profile=orders",
                "CharacterEncoding": 2
              }
            }
            """;
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new MimeStringContentTypeConverter());

        // Act
        Message? message = JsonConvert.DeserializeObject<Message>(JSON, settings);

        // Assert
        await Assert.That(message).IsNotNull();
        await Assert.That(message.Header.MessageId).IsEqualTo(new Id("order-42"));
        await Assert.That(message.Header.Topic).IsEqualTo(new RoutingKey("orders"));
        await Assert.That(message.Header.ContentType.MediaType).IsEqualTo("application/json");
        await Assert.That(message.Header.ContentType.CharSet).IsEqualTo("utf-8");
        await Assert.That(message.Header.ContentType.Parameters["profile"]).IsEqualTo("orders");
        await Assert.That(message.Body.ContentType).IsNotNull();
        await Assert.That(message.Body.ContentType.MediaType).IsEqualTo("application/json");
        await Assert.That(message.Body.ContentType.CharSet).IsEqualTo("utf-8");
        await Assert.That(message.Body.ContentType.Parameters["profile"]).IsEqualTo("orders");
        await Assert.That(message.Body.Value).IsEqualTo("hello");
        await Assert.That(message.Body.Memory.ToArray()).IsEqualTo("hello"u8.ToArray());
    }
}
