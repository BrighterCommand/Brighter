#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Actions;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Retry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy;

public class ResiliencePumpActionTests
{
    public static TheoryData<bool, bool, bool, bool, string> ActionCases
    {
        get
        {
            var cases = new TheoryData<bool, bool, bool, bool, string>();
            foreach (var isAsync in new[] { false, true })
                foreach (var typed in new[] { false, true })
                    foreach (var withContext in new[] { false, true })
                        foreach (var circuitBreaker in new[] { false, true })
                            foreach (var action in new[] { "reject", "defer", "dont-ack", "invalid" })
                                cases.Add(isAsync, typed, withContext, circuitBreaker, action);
            return cases;
        }
    }

    public static TheoryData<bool, bool, bool, bool> PipelineCases
    {
        get
        {
            var cases = new TheoryData<bool, bool, bool, bool>();
            foreach (var isAsync in new[] { false, true })
                foreach (var typed in new[] { false, true })
                    foreach (var withContext in new[] { false, true })
                        foreach (var circuitBreaker in new[] { false, true })
                            cases.Add(isAsync, typed, withContext, circuitBreaker);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ActionCases))]
    public async Task When_handling_a_pump_action_should_bypass_resilience_failure_handling(
        bool isAsync, bool typed, bool withContext, bool circuitBreaker, string action)
    {
        //Arrange
        Exception exception = action switch
        {
            "reject" => new RejectMessageAction("permanent failure"),
            "defer" => new DeferMessageAction("try later", new InvalidOperationException("reason"), 1234),
            "dont-ack" => new DontAckAction("leave unacknowledged"),
            "invalid" => new InvalidMessageAction("invalid payload"),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        //Act
        var (failures, attempts) = await ExecuteAsync(isAsync, typed, withContext, circuitBreaker, exception);

        //Assert
        Assert.All(failures, failure => Assert.Same(exception, failure));
        Assert.Equal(circuitBreaker ? 3 : 1, attempts);
        Assert.Contains(isAsync ? nameof(ResilienceActionHandlerAsync) : nameof(ResilienceActionHandler), exception.StackTrace);
        if (exception is DeferMessageAction defer)
            Assert.Equal(TimeSpan.FromMilliseconds(1234), defer.Delay);
    }

    [Theory]
    [MemberData(nameof(PipelineCases))]
    public async Task When_handling_an_application_error_should_keep_resilience_behavior(
        bool isAsync, bool typed, bool withContext, bool circuitBreaker)
    {
        //Arrange
        var exception = new InvalidOperationException("application failure");

        //Act
        var (failures, attempts) = await ExecuteAsync(isAsync, typed, withContext, circuitBreaker, exception);

        //Assert
        Assert.Same(exception, failures[0]);
        Assert.Equal(circuitBreaker ? 2 : 4, attempts);
        if (circuitBreaker)
            Assert.IsType<BrokenCircuitException>(failures[2]);
    }

    [Theory]
    [MemberData(nameof(PipelineCases))]
    public async Task When_handling_cancellation_should_propagate_without_retrying_or_breaking_the_circuit(
        bool isAsync, bool typed, bool withContext, bool circuitBreaker)
    {
        //Arrange
        var exception = new OperationCanceledException("cancelled");

        //Act
        var (failures, attempts) = await ExecuteAsync(isAsync, typed, withContext, circuitBreaker, exception);

        //Assert
        Assert.All(failures, failure => Assert.Same(exception, failure));
        Assert.Equal(circuitBreaker ? 3 : 1, attempts);
    }

    private static async Task<(Exception?[] Failures, int Attempts)> ExecuteAsync(
        bool isAsync, bool typed, bool withContext, bool circuitBreaker, Exception exception)
    {
        using var pipelines = new ResiliencePipelineRegistry<string>();
        if (isAsync)
            RegisterPipeline<ResilienceActionCommandAsync>(pipelines, typed, circuitBreaker);
        else
            RegisterPipeline<ResilienceActionCommand>(pipelines, typed, circuitBreaker);

        using var cancellation = new CancellationTokenSource();
        using var contextCancellation = new CancellationTokenSource();
        var resilienceContext = withContext ? ResilienceContextPool.Shared.Get(contextCancellation.Token) : null;
        var context = new RequestContext { ResiliencePipeline = pipelines, ResilienceContext = resilienceContext };
        var failures = new Exception?[circuitBreaker ? 3 : 1];
        try
        {
            if (isAsync)
            {
                var request = new ResilienceActionCommandAsync(exception);
                var handler = new ResilienceExceptionPolicyHandlerAsync<ResilienceActionCommandAsync> { Context = context };
                handler.InitializeFromAttributeParams("pump-actions", typed);
                handler.SetSuccessor(new ResilienceActionHandlerAsync { Context = context });
                for (var i = 0; i < failures.Length; i++)
                    failures[i] = await Record.ExceptionAsync(() => handler.HandleAsync(request, cancellation.Token));
                Assert.Equal(withContext ? contextCancellation.Token : cancellation.Token, request.CancellationToken);
                return (failures, request.Attempts);
            }
            else
            {
                var request = new ResilienceActionCommand(exception);
                var handler = new ResilienceExceptionPolicyHandler<ResilienceActionCommand> { Context = context };
                handler.InitializeFromAttributeParams("pump-actions", typed);
                handler.SetSuccessor(new ResilienceActionHandler { Context = context });
                for (var i = 0; i < failures.Length; i++)
                    failures[i] = Record.Exception(() => handler.Handle(request));
                return (failures, request.Attempts);
            }
        }
        finally
        {
            if (resilienceContext != null)
                ResilienceContextPool.Shared.Return(resilienceContext);
        }
    }

    private static void RegisterPipeline<TRequest>(ResiliencePipelineRegistry<string> registry, bool typed, bool circuitBreaker)
    {
        if (typed)
        {
            registry.TryAddBuilder<TRequest>("pump-actions", (builder, _) =>
            {
                if (circuitBreaker)
                    builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<TRequest> { MinimumThroughput = 2 });
                else
                    builder.AddRetry(new RetryStrategyOptions<TRequest> { MaxRetryAttempts = 3, Delay = TimeSpan.Zero });
            });
        }
        else
        {
            registry.TryAddBuilder("pump-actions", (builder, _) =>
            {
                if (circuitBreaker)
                    builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions { MinimumThroughput = 2 });
                else
                    builder.AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3, Delay = TimeSpan.Zero });
            });
        }
    }
}
