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

using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CloudEvents;

public class CloudEventsOptionalDataContentTypeTests
{
    public static IEnumerable<object[]> Payloads()
    {
        foreach (var explicitNull in new[] { false, true })
        foreach (var useAsync in new[] { false, true })
        {
            foreach (var json in new[]
            {
                "{\"value\":1}",
                "[1,\"two\",null]",
                "\"hello\\n\\\"world\\\"\"",
                "\"\"",
                "12.5",
                "true",
                "false",
                "null"
            })
            {
                yield return new object[]
                {
                    explicitNull, useAsync, ",\"data\":" + json,
                    "application/json", Encoding.UTF8.GetBytes(json)
                };
            }

            foreach (var bytes in new[]
            {
                new byte[] { 0, 255, 128, 1 },
                Encoding.UTF8.GetBytes("{\"value\":1}"),
                Array.Empty<byte>()
            })
            {
                yield return new object[]
                {
                    explicitNull, useAsync, ",\"data_base64\":\"" + Convert.ToBase64String(bytes) + "\"",
                    "application/octet-stream", bytes
                };
            }

            yield return new object[]
            {
                explicitNull, useAsync, string.Empty, "application/octet-stream", Array.Empty<byte>()
            };
        }
    }

    [Theory]
    [MemberData(nameof(Payloads))]
    public async Task When_unwrapping_without_a_data_content_type_should_preserve_the_payload(
        bool explicitNull, bool useAsync, string payloadMember, string expectedContentType, byte[] expectedBody)
    {
        //Arrange
        var contentTypeMember = explicitNull ? ",\"datacontenttype\":null" : string.Empty;
        var envelope = "{\"specversion\":\"1.0\",\"id\":\"event-1\",\"source\":\"/example\","
            + "\"type\":\"example.created\"" + contentTypeMember + payloadMember + "}";
        var message = new Message(
            new MessageHeader(
                new Id("transport-id"),
                new RoutingKey("test-topic"),
                MessageType.MT_EVENT,
                correlationId: new Id("correlation-1"),
                contentType: new ContentType("application/cloudevents+json")),
            new MessageBody(envelope));
        message.Header.Bag["transport-metadata"] = "preserved";
        using var transformer = new CloudEventsTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CloudEventFormat.Json);

        //Act
        var result = useAsync
            ? await transformer.UnwrapAsync(message, CancellationToken.None)
            : transformer.Unwrap(message);

        //Assert
        Assert.NotSame(message, result);
        Assert.Equal(expectedBody, result.Body.Memory.ToArray());
        Assert.Equal(expectedContentType, result.Header.ContentType.MediaType);
        Assert.Equal(expectedContentType, result.Body.ContentType.MediaType);
        Assert.Equal(new Id("event-1"), result.Id);
        Assert.Equal("1.0", result.Header.SpecVersion);
        Assert.Equal(new Uri("/example", UriKind.Relative), result.Header.Source);
        Assert.Equal(new CloudEventsType("example.created"), result.Header.Type);
        Assert.Equal(message.Header.Topic, result.Header.Topic);
        Assert.Equal(message.Header.CorrelationId, result.Header.CorrelationId);
        Assert.Equal("preserved", result.Header.Bag["transport-metadata"]);
    }
}
