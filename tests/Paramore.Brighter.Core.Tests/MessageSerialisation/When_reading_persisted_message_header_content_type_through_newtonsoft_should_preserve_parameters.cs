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
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class PersistedMessageHeaderNewtonsoftContentTypeTests
{
    [Theory]
    [InlineData("""
        {"MessageId":"order-42","Topic":"orders","ContentType":{
            "Boundary":null,"CharSet":"utf-8","MediaType":"application/json","Name":null,
            "Parameters":[{"Key":"charset","Value":"utf-8"},{"Key":"profile","Value":"urn:example:orders"}]
        }}
        """)]
    [InlineData("""
        {"messageId":"order-42","topic":"orders","contentType":{
            "boundary":null,"charSet":"utf-8","mediaType":"application/json","name":null,
            "parameters":[{"key":"charset","value":"utf-8"},{"key":"profile","value":"urn:example:orders"}]
        }}
        """)]
    public void When_reading_persisted_message_header_content_type_through_newtonsoft_should_preserve_parameters(string json)
    {
        // Arrange
        const string EXPECTED_MEDIA_TYPE = "application/json";
        const string EXPECTED_CHAR_SET = "utf-8";
        const string EXPECTED_PROFILE = "urn:example:orders";

        // Act
        MessageHeader? header = JsonConvert.DeserializeObject<MessageHeader>(json);

        // Assert
        Assert.NotNull(header);
        Assert.Equal(new Id("order-42"), header.MessageId);
        Assert.Equal(new RoutingKey("orders"), header.Topic);
        Assert.Equal(EXPECTED_MEDIA_TYPE, header.ContentType.MediaType);
        Assert.Equal(EXPECTED_CHAR_SET, header.ContentType.CharSet);
        Assert.Equal(EXPECTED_CHAR_SET, header.ContentType.Parameters["charset"]);
        Assert.Equal(EXPECTED_PROFILE, header.ContentType.Parameters["profile"]);
        Assert.Equal(2, header.ContentType.Parameters.Count);
    }
}
