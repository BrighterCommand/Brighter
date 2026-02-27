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
using Paramore.Brighter.Defer.Handlers;

namespace Paramore.Brighter.Defer.Attributes;

/// <summary>
/// Attribute that adds an async handler to catch unhandled exceptions and convert them to <see cref="Actions.DeferMessageAction"/>.
/// When used with a message pump (Reactor or Proactor), this causes the message to be requeued with a delay.
/// </summary>
/// <remarks>
/// <para>
/// This is the async version of <see cref="DeferMessageOnErrorAttribute"/>. Use this attribute with async handlers.
/// </para>
/// <para>
/// This attribute should be placed at the outermost position in the pipeline (lowest step number, typically 0)
/// to catch any exceptions that escape other handlers like retry policies or circuit breakers.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// [DeferMessageOnErrorAsync(step: 0, delayMilliseconds: 5000)]  // Outermost - catches anything, requeues with 5s delay
/// [UsePolicyAsync("RetryPolicy", step: 2)]                      // Retries first
/// public override async Task&lt;MyMessage&gt; HandleAsync(MyMessage message, CancellationToken cancellationToken)
/// {
///     // Business logic - if this fails after retries, message is requeued
///     return await base.HandleAsync(message, cancellationToken);
/// }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public class DeferMessageOnErrorAsyncAttribute : RequestHandlerAttribute
{
    private readonly int _delayMilliseconds;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeferMessageOnErrorAsyncAttribute"/> class.
    /// </summary>
    /// <param name="step">The step in the pipeline. Use a low number (e.g., 0) for outermost position.</param>
    /// <param name="delayMilliseconds">The delay in milliseconds before the message is requeued. Zero means use subscription default.</param>
    public DeferMessageOnErrorAsyncAttribute(int step, int delayMilliseconds = 0)
        : base(step, HandlerTiming.Before)
    {
        _delayMilliseconds = delayMilliseconds;
    }

    /// <summary>
    /// Gets the initializer parameters to pass to the handler.
    /// </summary>
    /// <returns>An array containing the delay in milliseconds.</returns>
    public override object[] InitializerParams()
    {
        return [_delayMilliseconds];
    }

    /// <summary>
    /// Gets the type of the handler that implements this attribute's behavior.
    /// </summary>
    /// <returns>The <see cref="DeferMessageOnErrorHandlerAsync{TRequest}"/> type.</returns>
    public override Type GetHandlerType()
        => typeof(DeferMessageOnErrorHandlerAsync<>);
}
