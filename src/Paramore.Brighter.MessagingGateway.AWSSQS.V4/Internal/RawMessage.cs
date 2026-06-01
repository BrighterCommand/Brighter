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
using System.Collections.Generic;
using Amazon.Runtime;
using Amazon.SQS.Model;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4.Internal;

/// <summary>
/// Mirrors <see cref="Amazon.SQS.Model.Message"/> but exposes the body as the raw
/// UTF-8 bytes that came off the wire, rather than as a <see cref="string"/>. The
/// Brighter pipeline takes <see cref="ReadOnlyMemory{Byte}"/> directly, so this
/// avoids the SDK's per-message string allocation and the round-trip back to bytes
/// that <see cref="MessageBody"/> would otherwise perform.
/// </summary>
internal sealed class RawMessage
{
    public string? MessageId { get; set; }
    public string? ReceiptHandle { get; set; }
    public string? MD5OfBody { get; set; }
    public string? MD5OfMessageAttributes { get; set; }

    /// <summary>
    /// The message body as UTF-8 bytes. Backed by an array allocated for this
    /// message; safe to retain past the receive call.
    /// </summary>
    public ReadOnlyMemory<byte> BodyBytes { get; set; }

    public Dictionary<string, string> Attributes { get; set; } = new();

    public Dictionary<string, MessageAttributeValue> MessageAttributes { get; set; } = new();
}

internal sealed class ReceiveMessageRawResponse : AmazonWebServiceResponse
{
    public List<RawMessage> Messages { get; set; } = new();
}
