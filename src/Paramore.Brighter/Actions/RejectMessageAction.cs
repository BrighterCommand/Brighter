#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
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
/// Thrown to indicate that a message should be rejected. Its purpose is end processing of a message. If there is a DLQ
/// then the message will move to the DLQ. Call `throw new RejectMessageAction()` from within a
/// <see cref="RequestHandler{TRequest}"/> or <see cref="RequestHandlerAsync{TRequest}"/> to end processing of a message.
/// The provided `reason` can be used to indicate the reason for the failure. 
/// </summary>
/// Although it seems we are using an exception for "flow control", we do not recommend using `RejectMessageAction` to
/// move messages that cannot be processed to a DLQ, instead just treating it as an application error. However, for those
/// with workflows that operate around the DLQ for monitoring errors, as this should be an exceptional path, we provide
/// it for use.
/// <remarks>
/// 
/// </remarks>
public class RejectMessageAction : Exception
{
    /// <summary>
    /// Throw to indicate that a <see cref="Message"/> should be rejected.
    /// </summary>
    public RejectMessageAction() {}
    
    /// <summary>
    /// Throw to indicate that a <see cref="Message"/> should be rejected.
    /// </summary>
    /// <param name="reason">The reason that a <see cref="Message"/> should be rejected</param>
    public RejectMessageAction(string? reason) : base(reason) {}
    
    /// <summary>
    /// Throw to indicate that a <see cref="Message"/> should be rejected.
    /// </summary>
    /// <param name="reason">The reason that a <see cref="Message"/> should be rejected</param>
    /// <param name="innerException">The exception that led to rejection of the <see cref="Message"/></param>
    public RejectMessageAction(string? reason, Exception? innerException) : base(reason, innerException) {}
}
