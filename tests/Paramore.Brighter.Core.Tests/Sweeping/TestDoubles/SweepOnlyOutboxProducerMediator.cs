using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// The base for mediator doubles used by sweeper tests: a sweep only calls
/// <see cref="ClearOutstandingFromOutboxAsync"/>, so every other member throws <see cref="NotSupportedException"/>.
/// </summary>
public abstract class SweepOnlyOutboxProducerMediator : IAmAnOutboxProducerMediator
{
    public IAmAnOutbox? Outbox => null;

    public abstract Task ClearOutstandingFromOutboxAsync(int amountToClear,
        TimeSpan minimumAge,
        bool useBulk,
        RequestContext requestContext,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default);

    public void CallViaExternalBus<T, TResponse>(Message outMessage, RequestContext? requestContext)
        where T : class, ICall where TResponse : class, IResponse => throw new NotSupportedException();

    public void ClearOutbox(Id[] posts, RequestContext requestContext, Dictionary<string, object>? args = null) =>
        throw new NotSupportedException();

    public Task ClearOutboxAsync(IEnumerable<Id> posts, RequestContext requestContext,
        bool continueOnCapturedContext = true, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Message CreateMessageFromRequest<TRequest>(TRequest request, RequestContext requestContext)
        where TRequest : class, IRequest => throw new NotSupportedException();

    public Task<Message> CreateMessageFromRequestAsync<TRequest>(TRequest request, RequestContext requestContext,
        CancellationToken cancellationToken) where TRequest : class, IRequest => throw new NotSupportedException();

    public void CreateRequestFromMessage<TRequest>(Message message, RequestContext? requestContext, out TRequest request)
        where TRequest : class, IRequest => throw new NotSupportedException();

    public bool HasAsyncOutbox() => false;

    public bool HasOutbox() => false;

    public void Dispose() { }
}
