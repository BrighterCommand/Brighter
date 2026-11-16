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

public class CloudEventsJsonStringRoundTripTests
{
    [Theory]
    [InlineData("application/json", "null", false, false)]
    [InlineData("application/json", "123", true, true)]
    [InlineData("application/json", "quoted \"hello\"\nbackslash \\ café", false, true)]
    [InlineData("application/json", "", true, false)]
    [InlineData("application/vnd.example+json; charset=utf-8", "null", false, false)]
    [InlineData("application/vnd.example+json; charset=utf-8", "123", true, true)]
    [InlineData("application/vnd.example+json; charset=utf-8", "quoted \"hello\"\nbackslash \\ café", false, true)]
    [InlineData("application/vnd.example+json; charset=utf-8", "", true, false)]
    public async Task When_round_tripping_json_string_data_should_preserve_the_string_value(
        string contentType, string value, bool wrapAsync, bool unwrapAsync)
    {
        //Arrange
        var payload = JsonSerializer.Serialize(value);
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("Test.Topic"), MessageType.MT_EVENT,
                contentType: new ContentType(contentType)),
            new MessageBody(payload));
        var transformer = new CloudEventsTransformer();
        transformer.InitializeWrapFromAttributeParams(null, null, null, null, null, null, CloudEventFormat.Json);
        transformer.InitializeUnwrapFromAttributeParams(CloudEventFormat.Json);
        var publication = new Publication();

        //Act
        var wrapped = wrapAsync
            ? await transformer.WrapAsync(message, publication, CancellationToken.None)
            : transformer.Wrap(message, publication);
        var unwrapped = unwrapAsync
            ? await transformer.UnwrapAsync(wrapped, CancellationToken.None)
            : transformer.Unwrap(wrapped);

        //Assert
        Assert.Equal(payload, unwrapped.Body.Value);
        using var json = JsonDocument.Parse(unwrapped.Body.Memory);
        Assert.Equal(JsonValueKind.String, json.RootElement.ValueKind);
        Assert.Equal(value, json.RootElement.GetString());
        Assert.Equal(new ContentType(contentType), unwrapped.Header.ContentType);
    }
}
