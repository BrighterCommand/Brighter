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

using System.Net.Mime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Paramore.Brighter.Core.Tests.MessageSerialisation.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class CustomContentTypePersistedFormatTests
{
    [Fact]
    public void When_serializing_a_message_with_a_custom_content_type_converter_should_preserve_its_persisted_format()
    {
        // Arrange
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new MimeStringContentTypeConverter());
        var contentType = new ContentType("application/json; charset=utf-8; profile=orders");
        var header = new MessageHeader(new Id("order-42"), new RoutingKey("orders"), MessageType.MT_EVENT,
            contentType: contentType);
        var message = new Message(header, new MessageBody("hello", contentType));

        // Act
        var json = JObject.Parse(JsonConvert.SerializeObject(message, settings));

        // Assert
        Assert.Equal(JTokenType.String, json["Header"]!["ContentType"]!.Type);
        Assert.Equal(contentType.ToString(), json["Header"]!["ContentType"]!.Value<string>());
        Assert.Equal(JTokenType.String, json["Body"]!["ContentType"]!.Type);
        Assert.Equal(contentType.ToString(), json["Body"]!["ContentType"]!.Value<string>());
    }
}
