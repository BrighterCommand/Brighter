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

namespace Paramore.Brighter.Core.Tests.CloudEvents;

public class CloudEventsJsonMediaTypeTests
{
    [Test]
    [Arguments("application/problem+json", "{\"status\":400}", JsonValueKind.Object, false)]
    [Arguments("application/problem+json", "{\"status\":400}", JsonValueKind.Object, true)]
    [Arguments("application/vnd.example+json", "{\"status\":400}", JsonValueKind.Object, false)]
    [Arguments("application/problem+json; charset=utf-8", "{\"status\":400}", JsonValueKind.Object, false)]
    [Arguments("APPLICATION/PROBLEM+JSON; charset=utf-8", "{\"status\":400}", JsonValueKind.Object, true)]
    [Arguments("application/json", "{\"status\":400}", JsonValueKind.Object, false)]
    [Arguments("text/json; charset=utf-8", "{\"status\":400}", JsonValueKind.Object, false)]
    [Arguments("APPLICATION/JSON", "{\"status\":400}", JsonValueKind.Object, false)]
    [Arguments("model/json", "{\"status\":400}", JsonValueKind.Object, false)]
    [Arguments("application/vnd.example+json; profile=\"https://example.org/schema\"", "[1,2]", JsonValueKind.Array, true)]
    [Arguments("application/problem+json", "\"problem\"", JsonValueKind.String, false)]
    [Arguments("application/problem+json", "400", JsonValueKind.Number, false)]
    [Arguments("application/problem+json", "true", JsonValueKind.True, false)]
    [Arguments("application/problem+json", "false", JsonValueKind.False, false)]
    [Arguments("application/problem+json", "null", JsonValueKind.Null, false)]
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
        await Assert.That(data.ValueKind).IsEqualTo(expectedKind);
        await Assert.That(JsonSerializer.Serialize(data)).IsEqualTo(JsonSerializer.Serialize(expected.RootElement));
        await Assert.That(envelope.RootElement.GetProperty("datacontenttype").GetString()).IsEqualTo(dataContentType.ToString());
        await Assert.That(wrapped.Header.ContentType.MediaType).IsEqualTo("application/cloudevents+json");
    }
}
