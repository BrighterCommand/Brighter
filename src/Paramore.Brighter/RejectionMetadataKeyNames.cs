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

namespace Paramore.Brighter;

/// <summary>
/// The well-known keys a transport stamps into <see cref="MessageHeader.Bag"/> when it routes a
/// rejected message to a dead-letter or invalid-message destination.
/// </summary>
/// <remarks>
/// Spelled exactly as the transports' <c>RefreshMetadata</c> methods stamp them. The presence of
/// <see cref="RejectionReason"/> in the bag marks a Brighter-routed rejection copy
/// (ADR 0077, "Delivery Count Contract").
/// </remarks>
public static class RejectionMetadataKeyNames
{
    /// <summary>
    /// Key for the rejection reason (<c>"rejectionReason"</c>).
    /// </summary>
    /// <remarks>
    /// Stamped with the name of the <see cref="Paramore.Brighter.RejectionReason"/> value.
    /// </remarks>
    public const string RejectionReason = "rejectionReason";

    /// <summary>
    /// Key for the human-readable rejection description (<c>"rejectionMessage"</c>).
    /// </summary>
    public const string RejectionMessage = "rejectionMessage";

    /// <summary>
    /// Key for the ISO 8601 UTC timestamp at which the rejection was processed (<c>"rejectionTimestamp"</c>).
    /// </summary>
    /// <remarks>
    /// Formatted with the round-trip specifier <c>"o"</c>.
    /// </remarks>
    public const string RejectionTimestamp = "rejectionTimestamp";

    /// <summary>
    /// Key for the original topic (routing key) of the message before it was routed to the dead-letter
    /// destination (<c>"originalTopic"</c>).
    /// </summary>
    /// <remarks>
    /// Stamped from <see cref="MessageHeader.Topic"/> at the point of rejection.
    /// </remarks>
    public const string OriginalTopic = "originalTopic";

    /// <summary>
    /// Key for the original message type of the message before routing (<c>"originalMessageType"</c>).
    /// </summary>
    /// <remarks>
    /// Stamped with the name of the <see cref="MessageType"/> value.
    /// </remarks>
    public const string OriginalMessageType = "originalMessageType";
}
