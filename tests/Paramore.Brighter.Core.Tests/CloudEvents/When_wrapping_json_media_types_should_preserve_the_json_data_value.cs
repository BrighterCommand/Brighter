#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia

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

using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CloudEvents;

public class CloudEventsJsonMediaTypeTests
{
    [Theory]
    [InlineData("application/problem+json", "{\"status\":400}", JsonValueKind.Object, false)]
    [InlineData("application/problem+json", "{\"status\":400}", JsonValueKind.Object, true)]
    [InlineData("application/vnd.example+json", "{\"status\":400}", JsonValueKind.Object, false)]
    [InlineData("application/problem+json; charset=utf-8", "{\"status\":400}", JsonValueKind.Object, false)]
    [InlineData("APPLICATION/PROBLEM+JSON; charset=utf-8", "{\"status\":400}", JsonValueKind.Object, true)]
    [InlineData("application/json", "{\"status\":400}", JsonValueKind.Object, false)]
    [InlineData("text/json; charset=utf-8", "{\"status\":400}", JsonValueKind.Object, false)]
    [InlineData("APPLICATION/JSON", "{\"status\":400}", JsonValueKind.Object, false)]
    [InlineData("model/json", "{\"status\":400}", JsonValueKind.Object, false)]
    [InlineData("application/vnd.example+json; profile=\"https://example.org/schema\"", "[1,2]", JsonValueKind.Array, true)]
    [InlineData("application/problem+json", "\"problem\"", JsonValueKind.String, false)]
    [InlineData("application/problem+json", "400", JsonValueKind.Number, false)]
    [InlineData("application/problem+json", "true", JsonValueKind.True, false)]
    [InlineData("application/problem+json", "false", JsonValueKind.False, false)]
    [InlineData("application/problem+json", "null", JsonValueKind.Null, false)]
    public async Task When_wrapping_json_media_types_should_preserve_the_json_data_value(
        string contentType, string payload, JsonValueKind expectedKind, bool useAsync)
    {
        //Arrange
        var dataContentType = new ContentType(contentType);
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("Test.Topic"), MessageType.MT_EVENT,
                contentType: dataContentType),
            new MessageBody(payload));
        var transformer = new CloudEventsTransformer();
        transformer.InitializeWrapFromAttributeParams(null, null, null, null, null, null, CloudEventFormat.Json);
        var publication = new Publication();

        //Act
        var wrapped = useAsync
            ? await transformer.WrapAsync(message, publication, CancellationToken.None)
            : transformer.Wrap(message, publication);

        //Assert
        using var envelope = JsonDocument.Parse(wrapped.Body.Memory);
        using var expected = JsonDocument.Parse(payload);
        var data = envelope.RootElement.GetProperty("data");
        Assert.Equal(expectedKind, data.ValueKind);
        Assert.Equal(JsonSerializer.Serialize(expected.RootElement), JsonSerializer.Serialize(data));
        Assert.Equal(dataContentType.ToString(), envelope.RootElement.GetProperty("datacontenttype").GetString());
        Assert.Equal("application/cloudevents+json", wrapped.Header.ContentType.MediaType);
    }
}
