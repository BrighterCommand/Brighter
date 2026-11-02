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

public class CloudEventsNonJsonMediaTypeTests
{
    [Theory]
    [InlineData("text/plain", "plain text with \"quotes\"\nand a newline", false)]
    [InlineData("text/plain", "{\"status\":400}", true)]
    [InlineData("application/jsonp", "{\"status\":400}", false)]
    [InlineData("text/json-seq", "{\"status\":400}", false)]
    [InlineData("application/problem+json-seq", "{\"status\":400}", false)]
    [InlineData("text/plain; profile=\"application/json\"", "{\"status\":400}", false)]
    [InlineData("text/plain; profile=\"application/problem+json\"", "{\"status\":400}", true)]
    public async Task When_wrapping_non_json_media_types_should_preserve_data_as_text(
        string contentType, string payload, bool useAsync)
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
        var data = envelope.RootElement.GetProperty("data");
        Assert.Equal(JsonValueKind.String, data.ValueKind);
        Assert.Equal(payload, data.GetString());
        Assert.Equal(dataContentType.ToString(), envelope.RootElement.GetProperty("datacontenttype").GetString());
        Assert.Equal("application/cloudevents+json", wrapped.Header.ContentType.MediaType);
    }
}
