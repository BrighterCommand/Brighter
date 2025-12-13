#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter;

/// <summary>
/// Indicates that the channel does not have native support for a DLQ, and that instead the consumer will provision
/// a dead letter channel. If a dead letter channel is defined, Brighter will produce messages to it from
/// a `MessagePump` on a call to `Reject`.
/// We call `Reject` in response to a `DeferMessageAction` exceeding the permitted number of retries, or the code calls
/// `RejectMessageAction` to force the message to `Reject` and then be placed on the DLQ. 
/// </summary>
/// <remarks>
/// When deriving from <see cref="Subscription"/> when middleware does not have native support for a DLQ,
/// use this interface to indicate that your consumer can forward messages to a DLQ
/// </remarks>
public interface IUseBrighterDeadLetterSupport
{
    /// <summary>
    /// The Routing Key used for the Dead Letter Channel
    /// </summary>
    RoutingKey? DeadLetterRoutingKey { get; set; }
}
