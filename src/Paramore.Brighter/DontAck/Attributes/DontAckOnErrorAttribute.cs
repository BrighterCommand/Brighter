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
using Paramore.Brighter.DontAck.Handlers;

namespace Paramore.Brighter.DontAck.Attributes;

/// <summary>
/// Attribute that adds a handler to catch unhandled exceptions and convert them to <see cref="Actions.DontAckAction"/>.
/// When used with a message pump (Reactor or Proactor), this causes the message to remain unacknowledged on the channel,
/// allowing the transport to re-deliver it after its visibility timeout expires.
/// </summary>
/// <remarks>
/// <para>
/// This attribute should be placed at the outermost position in the pipeline (lowest step number, typically 0)
/// to catch any exceptions that escape other handlers like retry policies or circuit breakers.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// [DontAckOnError(step: 0)]                  // Outermost - catches anything
/// [UsePolicy("RetryPolicy", step: 2)]        // Retries first
/// public override MyMessage Handle(MyMessage message)
/// {
///     // Business logic - if this fails after retries, message stays on the channel
///     return base.Handle(message);
/// }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public class DontAckOnErrorAttribute : RequestHandlerAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DontAckOnErrorAttribute"/> class.
    /// </summary>
    /// <param name="step">The step in the pipeline. Use a low number (e.g., 0) for outermost position.</param>
    public DontAckOnErrorAttribute(int step)
        : base(step, HandlerTiming.Before) { }

    /// <summary>
    /// Gets the type of the handler that implements this attribute's behavior.
    /// </summary>
    /// <returns>The <see cref="DontAckOnErrorHandler{TRequest}"/> type.</returns>
    public override Type GetHandlerType()
        => typeof(DontAckOnErrorHandler<>);
}
