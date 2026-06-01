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

using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.SQS.Model.Internal.MarshallTransformations;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4.Internal;

/// <summary>
/// An <see cref="AmazonSQSClient"/> subclass that adds <see cref="ReceiveMessageRawAsync"/>:
/// a ReceiveMessage variant whose response carries the message body as raw UTF-8
/// bytes (<see cref="ReadOnlyMemory{Byte}"/>) rather than a <see cref="string"/>.
/// All other operations behave identically to <see cref="AmazonSQSClient"/>.
/// </summary>
internal sealed class RawAmazonSQSClient : AmazonSQSClient
{
    public RawAmazonSQSClient(AWSCredentials credentials, AmazonSQSConfig config)
        : base(credentials, config)
    {
    }

    /// <summary>
    /// Issues a ReceiveMessage call and returns the response with each message body
    /// captured as raw UTF-8 bytes, avoiding the string allocation that the standard
    /// <see cref="AmazonSQSClient.ReceiveMessageAsync(ReceiveMessageRequest, CancellationToken)"/>
    /// performs for <see cref="Amazon.SQS.Model.Message.Body"/>.
    /// </summary>
    public Task<ReceiveMessageRawResponse> ReceiveMessageRawAsync(
        ReceiveMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var options = new InvokeOptions
        {
            RequestMarshaller = ReceiveMessageRequestMarshaller.Instance,
            ResponseUnmarshaller = ReceiveMessageRawResponseUnmarshaller.Instance,
        };

        return InvokeAsync<ReceiveMessageRawResponse>(request, options, cancellationToken);
    }
}
