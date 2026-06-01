#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Net;
using Amazon.Runtime;
using Amazon.Runtime.Internal.Transform;
using Amazon.Runtime.Internal.Util;
using Amazon.SQS.Model.Internal.MarshallTransformations;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4.Internal;

/// <summary>
/// Mirrors the SDK's <c>ReceiveMessageResponseUnmarshaller</c> but produces a
/// <see cref="ReceiveMessageRawResponse"/> by routing each message through
/// <see cref="RawMessageUnmarshaller"/>.
/// </summary>
internal sealed class ReceiveMessageRawResponseUnmarshaller : JsonResponseUnmarshaller
{
    public static ReceiveMessageRawResponseUnmarshaller Instance { get; } = new();

    private ReceiveMessageRawResponseUnmarshaller() { }

    public override AmazonWebServiceResponse Unmarshall(JsonUnmarshallerContext context)
    {
        var response = new ReceiveMessageRawResponse();
        var reader = new StreamingUtf8JsonReader(context.Stream);
        context.Read(ref reader);

        var depth = context.CurrentDepth;
        while (context.ReadAtDepth(depth, ref reader))
        {
            if (context.TestExpression("Messages", depth))
            {
                var listUnmarshaller = new JsonListUnmarshaller<RawMessage, RawMessageUnmarshaller>(
                    RawMessageUnmarshaller.Instance);
                response.Messages = listUnmarshaller.Unmarshall(context, ref reader) ?? new();
            }
        }

        return response;
    }

    public override AmazonServiceException UnmarshallException(
        JsonUnmarshallerContext context,
        System.Exception innerException,
        HttpStatusCode statusCode)
    {
        // Delegate to the SDK-supplied unmarshaller so that service-specific error
        // types (QueueDoesNotExist, etc.) are produced exactly as they would be for
        // the standard ReceiveMessage call.
        return ReceiveMessageResponseUnmarshaller.Instance.UnmarshallException(context, innerException, statusCode);
    }
}
