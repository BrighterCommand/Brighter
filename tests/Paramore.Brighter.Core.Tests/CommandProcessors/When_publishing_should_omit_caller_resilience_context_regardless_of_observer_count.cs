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
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.Registry;
using Polly.Retry;


namespace Paramore.Brighter.Core.Tests.CommandProcessors;

public class PublishResilienceContextTests
{
    public static IEnumerable<(bool, bool, int, bool)> PublishCases
    {
        get
        {
            var cases = new List<(bool, bool, int, bool)>();
            foreach (var isAsync in new[] { false, true })
                foreach (var typed in new[] { false, true })
                    foreach (var observerCount in new[] { 1, 2 })
                        foreach (var withContext in new[] { false, true })
                            cases.Add((isAsync, typed, observerCount, withContext));
            return cases;
        }
    }

    public static IEnumerable<(bool, bool, bool)> SendCases
    {
        get
        {
            var cases = new List<(bool, bool, bool)>();
            foreach (var isAsync in new[] { false, true })
                foreach (var typed in new[] { false, true })
                    foreach (var withContext in new[] { false, true })
                        cases.Add((isAsync, typed, withContext));
            return cases;
        }
    }

    [Test]
    [MethodDataSource(nameof(PublishCases))]
    public Task When_publishing_should_omit_caller_resilience_context_regardless_of_observer_count(
        bool isAsync, bool typed, int observerCount, bool withContext)
        => AssertDispatchAsync(isAsync, typed, observerCount, withContext, publish: true);

    [Test]
    [MethodDataSource(nameof(SendCases))]
    public Task When_sending_should_preserve_caller_resilience_context(
        bool isAsync, bool typed, bool withContext)
        => AssertDispatchAsync(isAsync, typed, observerCount: 1, withContext, publish: false);

    private static async Task AssertDispatchAsync(
        bool isAsync, bool typed, int observerCount, bool withContext, bool publish)
    {
        //Arrange
        using var methodCancellation = new CancellationTokenSource();
        using var contextCancellation = new CancellationTokenSource();
        var propertyKey = new ResiliencePropertyKey<string>("caller-data");
        var supplied = withContext
            ? ResilienceContextPool.Shared.Get("caller-operation", contextCancellation.Token)
            : null;
        supplied?.Properties.Set(propertyKey, "caller-value");

        try
        {
            using var pipelines = new ResiliencePipelineRegistry<string>();
            var retryObservations = new ConcurrentQueue<(
                bool UsesSuppliedContext, string? OperationKey, string? Property, CancellationToken Token)>();
            ValueTask ObserveRetry(ResilienceContext executionContext)
            {
                retryObservations.Enqueue((
                    ReferenceEquals(supplied, executionContext),
                    executionContext.OperationKey,
                    executionContext.Properties.GetValue(propertyKey, (string?)null),
                    executionContext.CancellationToken));
                return ValueTask.CompletedTask;
            }

            pipelines.TryAddBuilder("context-probe", (builder, _) => builder.AddRetry(
                new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
                    MaxRetryAttempts = 1,
                    Delay = TimeSpan.Zero,
                    OnRetry = args => ObserveRetry(args.Context)
                }));
            pipelines.TryAddBuilder<ResilienceContextProbe>("context-probe", (builder, _) => builder.AddRetry(
                new RetryStrategyOptions<ResilienceContextProbe>
                {
                    ShouldHandle = new PredicateBuilder<ResilienceContextProbe>().Handle<InvalidOperationException>(),
                    MaxRetryAttempts = 1,
                    Delay = TimeSpan.Zero,
                    OnRetry = args => ObserveRetry(args.Context)
                }));

            var caller = new RequestContext
            {
                ResilienceContext = supplied,
                Destination = new ProducerKey(new RoutingKey("caller-destination"))
            };
            caller.Bag["input"] = "original-value";
            var observations = new ConcurrentQueue<(
                Type Handler, int Attempt, IRequestContext Context, ResilienceContext? ResilienceContext,
                CancellationToken Token, bool CallerIntact, object Input, ProducerKey? Destination)>();
            var request = new ResilienceContextProbe((handler, attempt, context, token) =>
            {
                observations.Enqueue((handler, attempt, context, context.ResilienceContext, token,
                    ReferenceEquals(supplied, caller.ResilienceContext), context.Bag["input"], context.Destination));
                context.Bag[handler.Name] = "handled";
                if (attempt == 1)
                    throw new InvalidOperationException("transient handler failure");
            });

            var subscribers = new SubscriberRegistry();
            var firstHandler = HandlerType(isAsync, typed);
            subscribers.Add(typeof(ResilienceContextProbe), firstHandler);
            if (observerCount == 2)
                subscribers.Add(typeof(ResilienceContextProbe), HandlerType(isAsync, !typed));

            var factory = new SimpleHandlerFactory(
                type => type == typeof(ResilienceExceptionPolicyHandler<ResilienceContextProbe>)
                    ? new ResilienceExceptionPolicyHandler<ResilienceContextProbe>()
                    : type == typeof(TypedResilienceContextProbeHandler)
                        ? new TypedResilienceContextProbeHandler()
                        : type == typeof(ResilienceContextProbeHandler)
                            ? new ResilienceContextProbeHandler()
                            : throw new InvalidOperationException($"Unexpected handler type: {type}"),
                type => type == typeof(ResilienceExceptionPolicyHandlerAsync<ResilienceContextProbe>)
                    ? new ResilienceExceptionPolicyHandlerAsync<ResilienceContextProbe>()
                    : type == typeof(TypedResilienceContextProbeHandlerAsync)
                        ? new TypedResilienceContextProbeHandlerAsync()
                        : type == typeof(ResilienceContextProbeHandlerAsync)
                            ? new ResilienceContextProbeHandlerAsync()
                            : throw new InvalidOperationException($"Unexpected handler type: {type}"));
            var processor = new CommandProcessor(subscribers, factory,
                new InMemoryRequestContextFactory(), new PolicyRegistry(), pipelines, new InMemorySchedulerFactory());

            //Act
            if (isAsync)
            {
                if (publish)
                    await processor.PublishAsync(request, caller, cancellationToken: methodCancellation.Token);
                else
                    await processor.SendAsync(request, caller, cancellationToken: methodCancellation.Token);
            }
            else if (publish)
            {
                processor.Publish(request, caller);
            }
            else
            {
                processor.Send(request, caller);
            }

            //Assert
            var usesSuppliedContext = !publish && withContext;
            var expectedExecutionToken = usesSuppliedContext
                ? contextCancellation.Token
                : isAsync ? methodCancellation.Token : CancellationToken.None;
            await Assert.That(retryObservations.Count).IsEqualTo(observerCount);
            using (Assert.Multiple())
            {
                foreach (var observation in retryObservations)
                {
                    await Assert.That(observation.UsesSuppliedContext).IsEqualTo(usesSuppliedContext);
                    await Assert.That(observation.OperationKey).IsEqualTo(usesSuppliedContext ? "caller-operation" : null);
                    await Assert.That(observation.Property).IsEqualTo(usesSuppliedContext ? "caller-value" : null);
                    await Assert.That(observation.Token).IsEqualTo(expectedExecutionToken);
                }
            }
            await Assert.That(observations.Count).IsEqualTo(observerCount * 2);
            var observers = observations.GroupBy(observation => observation.Handler).ToArray();
            await Assert.That(observers.Length).IsEqualTo(observerCount);
            using (Assert.Multiple())
            {
                foreach (var observer in observers)
                {
                    await Assert.That(observer.Select(item => item.Attempt)).IsEquivalentTo(new[] { 1, 2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
                }
            }
            using (Assert.Multiple())
            {
                foreach (var observation in observations)
                {
                    await Assert.That(observation.ResilienceContext).IsSameReferenceAs(usesSuppliedContext ? supplied : null);
                    await Assert.That(observation.CallerIntact).IsTrue();
                    await Assert.That(observation.Input).IsEqualTo("original-value");
                    await Assert.That(observation.Context.ResiliencePipeline).IsSameReferenceAs(pipelines);
                    if (isAsync)
                        await Assert.That(observation.Token).IsEqualTo(expectedExecutionToken);
                    if (!publish)
                        await Assert.That(observation.Context).IsSameReferenceAs(caller);
                    if (observerCount == 1)
                        await Assert.That(observation.Destination).IsSameReferenceAs(caller.Destination);
                }
            }
            if (observerCount == 1)
            {
                await Assert.That(caller.Bag[firstHandler.Name]).IsEqualTo("handled");
            }
            else
            {
                await Assert.That(caller.Bag.Keys).DoesNotContain(firstHandler.Name);
                await Assert.That(caller.Bag.Keys).DoesNotContain(HandlerType(isAsync, !typed).Name);
                await Assert.That(observers[1].First().Context.Bag).IsNotSameReferenceAs(observers[0].First().Context.Bag);
            }

            await Assert.That(caller.ResilienceContext).IsSameReferenceAs(supplied);
            await Assert.That(caller.Bag["input"]).IsEqualTo("original-value");
            if (supplied != null)
            {
                await Assert.That(supplied.OperationKey).IsEqualTo("caller-operation");
                await Assert.That(supplied.Properties.GetValue(propertyKey, "")).IsEqualTo("caller-value");
                await Assert.That(supplied.CancellationToken).IsEqualTo(contextCancellation.Token);
            }
        }
        finally
        {
            if (supplied != null)
                ResilienceContextPool.Shared.Return(supplied);
        }
    }

    private static Type HandlerType(bool isAsync, bool typed)
        => isAsync
            ? typed ? typeof(TypedResilienceContextProbeHandlerAsync) : typeof(ResilienceContextProbeHandlerAsync)
            : typed ? typeof(TypedResilienceContextProbeHandler) : typeof(ResilienceContextProbeHandler);
}
