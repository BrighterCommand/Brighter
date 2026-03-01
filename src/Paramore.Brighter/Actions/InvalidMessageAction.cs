#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper

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

namespace Paramore.Brighter.Actions;

/// <summary>
/// Thrown to indicate that a message could not be deserialized and should be sent to an invalid message channel.
/// This exception is typically thrown by message mappers when deserialization fails due to schema mismatches,
/// versioning issues, or malformed message content. If an invalid message channel is configured, the message
/// will be routed there; otherwise, it falls back to the dead letter channel if available.
/// </summary>
/// <remarks>
/// Use this exception specifically for deserialization failures where the message content cannot be converted
/// to the expected request type. This differs from <see cref="RejectMessageAction"/> which is for messages that
/// can be deserialized but cannot be processed due to business logic or transient failures.
///
/// The message pump will catch this exception and route the message to:
/// 1. The configured invalid message channel (if available)
/// 2. The dead letter channel (if no invalid message channel is configured)
/// 3. Acknowledge and log (if neither channel is configured)
/// </remarks>
public class InvalidMessageAction : Exception
{
    /// <summary>
    /// Throw to indicate that a <see cref="Message"/> could not be deserialized.
    /// </summary>
    public InvalidMessageAction() {}

    /// <summary>
    /// Throw to indicate that a <see cref="Message"/> could not be deserialized.
    /// </summary>
    /// <param name="reason">The reason that deserialization failed</param>
    public InvalidMessageAction(string? reason) : base(reason) {}

    /// <summary>
    /// Throw to indicate that a <see cref="Message"/> could not be deserialized.
    /// </summary>
    /// <param name="reason">The reason that deserialization failed</param>
    /// <param name="innerException">The exception that caused the deserialization failure</param>
    public InvalidMessageAction(string? reason, Exception? innerException) : base(reason, innerException) {}
}
