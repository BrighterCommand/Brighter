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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Testing;

/// <summary>
/// A spy implementation of <see cref="IAmACommandProcessor"/> for testing.
/// Records all method calls for later verification.
/// </summary>
/// <remarks>
/// This type is not thread-safe. It is intended for unit tests where the handler under test
/// is invoked sequentially (including single-threaded async/await). If your handler spawns
/// concurrent tasks that call the command processor in parallel, coordinate access externally
/// or assert after all tasks have completed.
/// </remarks>
public class SpyCommandProcessor : IAmACommandProcessor
{
    private readonly List<RecordedCall> _recordedCalls = new();
    private readonly Queue<IRequest> _requests = new();
    private readonly Dictionary<Id, IRequest> _depositedRequests = new();

    /// <summary>
    /// Gets a read-only list of command types in the order they were called.
    /// </summary>
    public IReadOnlyList<CommandType> Commands => _recordedCalls.Select(c => c.Type).ToList().AsReadOnly();

    /// <summary>
    /// Gets a read-only list of all recorded calls with full details.
    /// </summary>
    public IReadOnlyList<RecordedCall> RecordedCalls => _recordedCalls.AsReadOnly();

    /// <summary>
    /// Gets a read-only dictionary of requests deposited to the outbox, keyed by their Id.
    /// Requests are added here when <see cref="DepositPost{TRequest}(TRequest, RequestContext?, Dictionary{string, object}?)"/> is called.
    /// They remain in this dictionary for the lifetime of the spy (or until <see cref="Reset"/> is called).
    /// When <see cref="ClearOutbox"/> is called, matching requests are also copied to the observation queue
    /// and become available via <see cref="Observe{T}"/>.
    /// </summary>
    public IReadOnlyDictionary<Id, IRequest> DepositedRequests => _depositedRequests;

    /// <summary>
    /// Resets all recorded state, clearing recorded calls, commands, observation queue, and deposited requests.
    /// Useful for reusing the spy across multiple test scenarios.
    /// </summary>
    public void Reset()
    {
        _recordedCalls.Clear();
        _requests.Clear();
        _depositedRequests.Clear();
    }

    /// <summary>
    /// Check if a specific method type was called at least once.
    /// </summary>
    /// <param name="type">The command type to check for.</param>
    /// <returns>True if the method was called at least once, false otherwise.</returns>
    public bool WasCalled(CommandType type) => _recordedCalls.Any(c => c.Type == type);

    /// <summary>
    /// Get the number of times a specific method type was called.
    /// </summary>
    /// <param name="type">The command type to count.</param>
    /// <returns>The number of times the method was called.</returns>
    public int CallCount(CommandType type) => _recordedCalls.Count(c => c.Type == type);

    /// <summary>
    /// Get all captured requests of the specified type without consuming them.
    /// Unlike <see cref="Observe{T}"/>, this method is non-destructive and can be called multiple times.
    /// </summary>
    /// <typeparam name="T">The type of request to retrieve.</typeparam>
    /// <returns>An enumerable of all requests of type T.</returns>
    public IEnumerable<T> GetRequests<T>() where T : class, IRequest
    {
        return _recordedCalls
            .Where(c => c.Request is T)
            .Select(c => (T)c.Request);
    }

    /// <summary>
    /// Get all recorded calls for a specific command type.
    /// Returns full <see cref="RecordedCall"/> objects with Type, Request, Timestamp, and Context.
    /// </summary>
    /// <param name="type">The command type to filter by.</param>
    /// <returns>An enumerable of all recorded calls matching the command type.</returns>
    public IEnumerable<RecordedCall> GetCalls(CommandType type)
    {
        return _recordedCalls.Where(c => c.Type == type);
    }

    /// <summary>
    /// Dequeue the next captured request of the specified type in FIFO order.
    /// Useful for sequential verification of multiple calls.
    /// </summary>
    /// <typeparam name="T">The type of request to observe.</typeparam>
    /// <returns>The next request of type T.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no request of type T is in the queue.</exception>
    public T Observe<T>() where T : class, IRequest
    {
        var queue = new Queue<IRequest>();
        while (_requests.Count > 0)
        {
            var request = _requests.Dequeue();
            if (request is T typed)
            {
                // Put remaining items back
                while (queue.Count > 0)
                {
                    _requests.Enqueue(queue.Dequeue());
                }
                return typed;
            }
            queue.Enqueue(request);
        }

        // Restore the queue
        while (queue.Count > 0)
        {
            _requests.Enqueue(queue.Dequeue());
        }

        throw new InvalidOperationException($"No request of type {typeof(T).Name} found in the queue.");
    }

    private void RecordCall(CommandType type, IRequest request, RequestContext? context = null)
    {
        _recordedCalls.Add(new RecordedCall(type, request, DateTime.UtcNow, context));
        _requests.Enqueue(request);
    }

    private void RecordDeposit(CommandType type, IRequest request, RequestContext? context = null)
    {
        _recordedCalls.Add(new RecordedCall(type, request, DateTime.UtcNow, context));
        _depositedRequests[request.Id] = request;
    }

    /// <inheritdoc />
    public virtual void Send<TRequest>(TRequest command, RequestContext? requestContext = null)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.Send, command, requestContext);
    }

    /// <inheritdoc />
    public virtual string Send<TRequest>(DateTimeOffset at, TRequest command, RequestContext? requestContext = null)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.Scheduler, command, requestContext);
        return Guid.NewGuid().ToString();
    }

    /// <inheritdoc />
    public virtual string Send<TRequest>(TimeSpan delay, TRequest command, RequestContext? requestContext = null)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.Scheduler, command, requestContext);
        return Guid.NewGuid().ToString();
    }

    /// <inheritdoc />
    public virtual Task SendAsync<TRequest>(TRequest command, RequestContext? requestContext = null,
        bool continueOnCapturedContext = true, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.SendAsync, command, requestContext);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task<string> SendAsync<TRequest>(DateTimeOffset at, TRequest command,
        RequestContext? requestContext = null, bool continueOnCapturedContext = true,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.SchedulerAsync, command, requestContext);
        return Task.FromResult(Guid.NewGuid().ToString());
    }

    /// <inheritdoc />
    public virtual Task<string> SendAsync<TRequest>(TimeSpan delay, TRequest command,
        RequestContext? requestContext = null, bool continueOnCapturedContext = true,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.SchedulerAsync, command, requestContext);
        return Task.FromResult(Guid.NewGuid().ToString());
    }

    /// <inheritdoc />
    public virtual void Publish<TRequest>(TRequest @event, RequestContext? requestContext = null)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.Publish, @event, requestContext);
    }

    /// <inheritdoc />
    public virtual string Publish<TRequest>(DateTimeOffset at, TRequest @event, RequestContext? requestContext = null)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.Scheduler, @event, requestContext);
        return Guid.NewGuid().ToString();
    }

    /// <inheritdoc />
    public virtual string Publish<TRequest>(TimeSpan delay, TRequest @event, RequestContext? requestContext = null)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.Scheduler, @event, requestContext);
        return Guid.NewGuid().ToString();
    }

    /// <inheritdoc />
    public virtual Task PublishAsync<TRequest>(TRequest @event, RequestContext? requestContext = null,
        bool continueOnCapturedContext = true, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.PublishAsync, @event, requestContext);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task<string> PublishAsync<TRequest>(DateTimeOffset at, TRequest @event,
        RequestContext? requestContext = null, bool continueOnCapturedContext = true,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.SchedulerAsync, @event, requestContext);
        return Task.FromResult(Guid.NewGuid().ToString());
    }

    /// <inheritdoc />
    public virtual Task<string> PublishAsync<TRequest>(TimeSpan delay, TRequest @event,
        RequestContext? requestContext = null, bool continueOnCapturedContext = true,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.SchedulerAsync, @event, requestContext);
        return Task.FromResult(Guid.NewGuid().ToString());
    }

    /// <inheritdoc />
    public virtual void Post<TRequest>(TRequest request, RequestContext? requestContext = null,
        Dictionary<string, object>? args = null)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.Post, request, requestContext);
    }

    /// <inheritdoc />
    public virtual string Post<TRequest>(DateTimeOffset at, TRequest request, RequestContext? requestContext = null,
        Dictionary<string, object>? args = null)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.Scheduler, request, requestContext);
        return Guid.NewGuid().ToString();
    }

    /// <inheritdoc />
    public virtual string Post<TRequest>(TimeSpan delay, TRequest request, RequestContext? requestContext = null,
        Dictionary<string, object>? args = null)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.Scheduler, request, requestContext);
        return Guid.NewGuid().ToString();
    }

    /// <inheritdoc />
    public virtual Task PostAsync<TRequest>(TRequest request, RequestContext? requestContext = null,
        Dictionary<string, object>? args = null, bool continueOnCapturedContext = true,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.PostAsync, request, requestContext);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task<string> PostAsync<TRequest>(DateTimeOffset at, TRequest request,
        RequestContext? requestContext = null, Dictionary<string, object>? args = null,
        bool continueOnCapturedContext = true, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.SchedulerAsync, request, requestContext);
        return Task.FromResult(Guid.NewGuid().ToString());
    }

    /// <inheritdoc />
    public virtual Task<string> PostAsync<TRequest>(TimeSpan delay, TRequest request,
        RequestContext? requestContext = null, Dictionary<string, object>? args = null,
        bool continueOnCapturedContext = true, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        RecordCall(CommandType.SchedulerAsync, request, requestContext);
        return Task.FromResult(Guid.NewGuid().ToString());
    }

    /// <inheritdoc />
    public virtual Id DepositPost<TRequest>(TRequest request, RequestContext? requestContext = null,
        Dictionary<string, object>? args = null)
        where TRequest : class, IRequest
    {
        RecordDeposit(CommandType.Deposit, request, requestContext);
        return request.Id;
    }

    /// <inheritdoc />
    public virtual Id DepositPost<TRequest, TTransaction>(TRequest request,
        IAmABoxTransactionProvider<TTransaction> transactionProvider, RequestContext? requestContext = null,
        Dictionary<string, object>? args = null, string? batchId = null)
        where TRequest : class, IRequest
    {
        RecordDeposit(CommandType.Deposit, request, requestContext);
        return request.Id;
    }

    /// <inheritdoc />
    public virtual Id[] DepositPost<TRequest>(IEnumerable<TRequest> requests, RequestContext? requestContext,
        Dictionary<string, object>? args = null)
        where TRequest : class, IRequest
    {
        var ids = new List<Id>();
        foreach (var request in requests)
        {
            RecordDeposit(CommandType.Deposit, request, requestContext);
            ids.Add(request.Id);
        }
        return ids.ToArray();
    }

    /// <inheritdoc />
    public virtual Id[] DepositPost<TRequest, TTransaction>(IEnumerable<TRequest> requests,
        IAmABoxTransactionProvider<TTransaction> transactionProvider, RequestContext? requestContext = null,
        Dictionary<string, object>? args = null)
        where TRequest : class, IRequest
    {
        var ids = new List<Id>();
        foreach (var request in requests)
        {
            RecordDeposit(CommandType.Deposit, request, requestContext);
            ids.Add(request.Id);
        }
        return ids.ToArray();
    }

    /// <inheritdoc />
    public virtual Task<Id> DepositPostAsync<TRequest>(TRequest request, RequestContext? requestContext = null,
        Dictionary<string, object>? args = null, bool continueOnCapturedContext = true,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        RecordDeposit(CommandType.DepositAsync, request, requestContext);
        return Task.FromResult(request.Id);
    }

    /// <inheritdoc />
    public virtual Task<Id> DepositPostAsync<T, TTransaction>(T request,
        IAmABoxTransactionProvider<TTransaction> transactionProvider, RequestContext? requestContext = null,
        Dictionary<string, object>? args = null, bool continueOnCapturedContext = true,
        CancellationToken cancellationToken = default, string? batchId = null)
        where T : class, IRequest
    {
        RecordDeposit(CommandType.DepositAsync, request, requestContext);
        return Task.FromResult(request.Id);
    }

    /// <inheritdoc />
    public virtual Task<Id[]> DepositPostAsync<TRequest>(IEnumerable<TRequest> requests,
        RequestContext? requestContext = null, Dictionary<string, object>? args = null,
        bool continueOnCapturedContext = true, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        var ids = new List<Id>();
        foreach (var request in requests)
        {
            RecordDeposit(CommandType.DepositAsync, request, requestContext);
            ids.Add(request.Id);
        }
        return Task.FromResult(ids.ToArray());
    }

    /// <inheritdoc />
    public virtual Task<Id[]> DepositPostAsync<T, TTransaction>(IEnumerable<T> requests,
        IAmABoxTransactionProvider<TTransaction> transactionProvider, RequestContext? requestContext = null,
        Dictionary<string, object>? args = null, bool continueOnCapturedContext = true,
        CancellationToken cancellationToken = default)
        where T : class, IRequest
    {
        var ids = new List<Id>();
        foreach (var request in requests)
        {
            RecordDeposit(CommandType.DepositAsync, request, requestContext);
            ids.Add(request.Id);
        }
        return Task.FromResult(ids.ToArray());
    }

    /// <summary>
    /// Records a <see cref="CommandType.Clear"/> call and moves previously deposited requests to the observation queue.
    /// To verify clears in tests, use <see cref="WasCalled"/>(<see cref="CommandType.Clear"/>) or
    /// <see cref="GetCalls"/>(<see cref="CommandType.Clear"/>). After clearing, deposited requests become
    /// available via <see cref="Observe{T}"/>.
    /// </summary>
    public virtual void ClearOutbox(Id[] ids, RequestContext? requestContext = null,
        Dictionary<string, object>? args = null)
    {
        // ClearOutbox doesn't have a request, so we create a synthetic one for tracking
        _recordedCalls.Add(new RecordedCall(CommandType.Clear, new ClearOutboxRequest(ids), DateTime.UtcNow, requestContext));

        // Move deposited requests to the observation queue
        foreach (var id in ids)
        {
            if (_depositedRequests.TryGetValue(id, out var request))
            {
                _requests.Enqueue(request);
            }
        }
    }

    /// <summary>
    /// Records a <see cref="CommandType.ClearAsync"/> call and moves previously deposited requests to the observation queue.
    /// To verify clears in tests, use <see cref="WasCalled"/>(<see cref="CommandType.ClearAsync"/>) or
    /// <see cref="GetCalls"/>(<see cref="CommandType.ClearAsync"/>). After clearing, deposited requests become
    /// available via <see cref="Observe{T}"/>.
    /// </summary>
    public virtual Task ClearOutboxAsync(IEnumerable<Id> posts, RequestContext? requestContext = null,
        Dictionary<string, object>? args = null, bool continueOnCapturedContext = true,
        CancellationToken cancellationToken = default)
    {
        var idArray = posts.ToArray();
        // ClearOutboxAsync doesn't have a request, so we create a synthetic one for tracking
        _recordedCalls.Add(new RecordedCall(CommandType.ClearAsync, new ClearOutboxRequest(idArray), DateTime.UtcNow, requestContext));

        // Move deposited requests to the observation queue
        foreach (var id in idArray)
        {
            if (_depositedRequests.TryGetValue(id, out var request))
            {
                _requests.Enqueue(request);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records a <see cref="CommandType.Call"/> and returns <c>null</c>.
    /// The spy does not process request-reply pipelines.
    /// </summary>
    /// <remarks>
    /// To provide canned responses, subclass <see cref="SpyCommandProcessor"/> and override this method:
    /// <code>
    /// class MyTestProcessor : SpyCommandProcessor
    /// {
    ///     public override TResponse? Call&lt;T, TResponse&gt;(T request, RequestContext? requestContext = null, TimeSpan? timeOut = null)
    ///     {
    ///         base.Call&lt;T, TResponse&gt;(request, requestContext, timeOut);
    ///         return new MyResponse(...) as TResponse;
    ///     }
    /// }
    /// </code>
    /// </remarks>
    /// <returns>Always <c>null</c>. Override to return test responses.</returns>
    public virtual TResponse? Call<T, TResponse>(T request, RequestContext? requestContext = null, TimeSpan? timeOut = null)
        where T : class, ICall
        where TResponse : class, IResponse
    {
        RecordCall(CommandType.Call, request, requestContext);
        return null;
    }

    /// <summary>
    /// Synthetic request type for tracking ClearOutbox calls.
    /// </summary>
    private sealed class ClearOutboxRequest : IRequest
    {
        public Id Id { get; set; } = Id.Random();
        public Id? CorrelationId { get; set; }
        public ReplyAddress? ReplyTo { get; set; }

        /// <summary>
        /// The Ids that were cleared from the outbox.
        /// </summary>
        public Id[] ClearedIds { get; }

        public ClearOutboxRequest(Id[] ids)
        {
            ClearedIds = ids;
        }
    }
}
