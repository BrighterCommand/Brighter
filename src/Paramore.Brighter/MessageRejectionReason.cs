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
/// Indicates why a message was rejected
/// </summary>
public enum RejectionReason
{
    /// <summary>
    /// Default, but presence would be an error
    /// </summary>
    None,
    /// <summary>
    /// The message could not be deserialized correctly
    /// </summary>
    Unacceptable,
    /// <summary>
    /// The message could not be delivered. It may have been retried multiple times
    /// </summary>
    DeliveryError,
}

/// <summary>
/// Indicates the reason that a message was rejected along with a description of why it was rejected. 
/// </summary>
/// <param name="RejectionReason">The <see cref="RejectionReason"/> indicating why this message was rejected</param>
/// <param name="Description">A description with more information about the reason for the rejection</param>
public record MessageRejectionReason(RejectionReason RejectionReason, string? Description = null);