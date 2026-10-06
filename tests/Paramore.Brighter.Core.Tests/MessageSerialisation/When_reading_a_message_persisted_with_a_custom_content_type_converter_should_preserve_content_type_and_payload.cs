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
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class CustomContentTypePersistedMessageTests
{
    [Fact]
    public void When_reading_a_message_persisted_with_a_custom_content_type_converter_should_preserve_content_type_and_payload()
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
        Assert.NotNull(message);
        Assert.Equal(new Id("order-42"), message.Header.MessageId);
        Assert.Equal(new RoutingKey("orders"), message.Header.Topic);
        Assert.Equal("application/json", message.Header.ContentType.MediaType);
        Assert.Equal("utf-8", message.Header.ContentType.CharSet);
        Assert.Equal("orders", message.Header.ContentType.Parameters["profile"]);
        Assert.NotNull(message.Body.ContentType);
        Assert.Equal("application/json", message.Body.ContentType.MediaType);
        Assert.Equal("utf-8", message.Body.ContentType.CharSet);
        Assert.Equal("orders", message.Body.ContentType.Parameters["profile"]);
        Assert.Equal("hello", message.Body.Value);
        Assert.Equal("hello"u8.ToArray(), message.Body.Memory.ToArray());
    }
}
