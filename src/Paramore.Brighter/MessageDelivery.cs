#region Licence
/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter;

/// <summary>
/// Coordinates claim-check cleanup with the outcome of one received message.
/// </summary>
/// <remarks>
/// Keep the unwrap pipeline alive until this delivery ends. Call Complete or CompleteAsync only
/// after successful dispatch and final transport acknowledgement. Requeue, nack and rejection
/// must dispose the delivery without completing it, so its luggage remains available for replay.
/// Standalone unwrap pipelines without a delivery retain their immediate-cleanup behavior.
/// Instances belong to one delivery and must not be shared between concurrent requests.
/// </remarks>
public sealed partial class MessageDelivery : IDisposable
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MessageDelivery>();
    private readonly RequestContext _context;
    private readonly List<(Action Delete, Func<CancellationToken, Task> DeleteAsync)> _cleanup = new();
    private bool _ended;

    /// <summary>Begins a delivery associated with the supplied request context.</summary>
    /// <param name="context">The context passed to the unwrap and handler pipelines.</param>
    /// <exception cref="ArgumentNullException">The context is null.</exception>
    /// <exception cref="InvalidOperationException">The context already has an active delivery.</exception>
    public MessageDelivery(RequestContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        if (context.Delivery is not null)
            throw new InvalidOperationException("The request context already has an active message delivery.");
        context.Delivery = this;
    }

    internal void OnAcknowledged(Action delete, Func<CancellationToken, Task> deleteAsync) =>
        _cleanup.Add((delete, deleteAsync));

    /// <summary>Deletes non-retained luggage after successful dispatch and final acknowledgement.</summary>
    /// <remarks>Cleanup failures are logged; an acknowledged message cannot be redelivered to retry cleanup.</remarks>
    public void Complete()
    {
        if (_ended) return;
        _ended = true;
        foreach (var cleanup in _cleanup)
        {
            try { cleanup.Delete(); }
            catch (Exception exception) { Log.FailedToDeleteLuggage(s_logger, exception); }
        }
        _cleanup.Clear();
    }

    /// <summary>Deletes non-retained luggage asynchronously after successful dispatch and final acknowledgement.</summary>
    /// <param name="cancellationToken">Cancels storage cleanup.</param>
    /// <returns>A task that completes after the cleanup attempts.</returns>
    /// <remarks>Cleanup failures are logged; an acknowledged message cannot be redelivered to retry cleanup.</remarks>
    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (_ended) return;
        _ended = true;
        foreach (var cleanup in _cleanup)
        {
            try { await cleanup.DeleteAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception exception) { Log.FailedToDeleteLuggage(s_logger, exception); }
        }
        _cleanup.Clear();
    }

    /// <summary>Ends the delivery without deleting any remaining luggage.</summary>
    public void Dispose()
    {
        _ended = true;
        _cleanup.Clear();
        if (ReferenceEquals(_context.Delivery, this))
            _context.Delivery = null;
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Failed to delete claim-check luggage after acknowledgement")]
        public static partial void FailedToDeleteLuggage(ILogger logger, Exception exception);
    }
}
