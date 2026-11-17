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

public class CloudEventsStringDataUnwrapTests
{
    [Theory]
    [InlineData("application/json", "hello", true, false)]
    [InlineData("application/json", "hello", true, true)]
    [InlineData("application/json", "null", true, false)]
    [InlineData("application/json", "null", true, true)]
    [InlineData("application/json", "123", true, false)]
    [InlineData("application/json", "123", true, true)]
    [InlineData("application/json", "true", true, false)]
    [InlineData("application/json", "", true, true)]
    [InlineData("application/json", "quoted \"hello\"\nbackslash \\ café", true, false)]
    [InlineData("application/json", "quoted \"hello\"\nbackslash \\ café", true, true)]
    [InlineData("application/problem+json", "hello", true, false)]
    [InlineData("APPLICATION/VND.EXAMPLE+JSON; charset=utf-8", "quoted \"hello\"\nnext line", true, true)]
    [InlineData("text/json; charset=utf-8", "123", true, false)]
    [InlineData("model/json", "null", true, true)]
    [InlineData("text/plain", "quoted \"hello\"\nbackslash \\ café", false, false)]
    [InlineData("text/plain", "quoted \"hello\"\nbackslash \\ café", false, true)]
    [InlineData("text/plain", "null", false, false)]
    [InlineData("application/jsonp", "123", false, true)]
    [InlineData("text/json-seq", "true", false, false)]
    [InlineData("text/plain; profile=\"application/json\"", "null", false, true)]
    public async Task When_unwrapping_string_data_should_preserve_its_declared_representation(
        string contentType, string value, bool expectJson, bool useAsync)
    {
        //Arrange
        var envelope = JsonSerializer.Serialize(new
        {
            specversion = "1.0",
            id = "string-event",
            source = "/example",
            type = "example.created",
            datacontenttype = contentType,
            data = value
        });
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("Test.Topic"), MessageType.MT_EVENT,
                contentType: new ContentType("application/cloudevents+json")),
            new MessageBody(envelope));
        var transformer = new CloudEventsTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CloudEventFormat.Json);
        var expectedBody = expectJson ? JsonSerializer.Serialize(value) : value;

        //Act
        var unwrapped = useAsync
            ? await transformer.UnwrapAsync(message, CancellationToken.None)
            : transformer.Unwrap(message);

        //Assert
        Assert.Equal(expectedBody, unwrapped.Body.Value);
        Assert.Equal(new ContentType(contentType), unwrapped.Header.ContentType);
        Assert.Equal("string-event", unwrapped.Header.MessageId.Value);
    }
}
