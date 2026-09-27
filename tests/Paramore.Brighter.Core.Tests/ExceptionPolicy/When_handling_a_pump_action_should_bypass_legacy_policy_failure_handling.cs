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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Actions;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy;

public class LegacyPolicyPumpActionTests
{
    public static TheoryData<bool, int, bool, string> ActionCases
    {
        get
        {
            var cases = new TheoryData<bool, int, bool, string>();
            foreach (var isAsync in new[] { false, true })
                foreach (var policyCount in new[] { 1, 2, 3 })
                    foreach (var circuitBreaker in new[] { false, true })
                        foreach (var action in new[] { "reject", "defer", "dont-ack", "invalid" })
                            cases.Add(isAsync, policyCount, circuitBreaker, action);
            return cases;
        }
    }

    public static TheoryData<bool, int, bool> PolicyCases
    {
        get
        {
            var cases = new TheoryData<bool, int, bool>();
            foreach (var isAsync in new[] { false, true })
                foreach (var policyCount in new[] { 1, 2, 3 })
                    foreach (var circuitBreaker in new[] { false, true })
                        cases.Add(isAsync, policyCount, circuitBreaker);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ActionCases))]
    public async Task When_handling_a_pump_action_should_bypass_legacy_policy_failure_handling(
        bool isAsync, int policyCount, bool circuitBreaker, string action)
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
        var (failures, attempts) = await ExecuteAsync(isAsync, policyCount, circuitBreaker, exception);

        //Assert
        Assert.All(failures, failure => Assert.Same(exception, failure));
        Assert.Equal(circuitBreaker ? 3 : 1, attempts);
        Assert.Contains(isAsync ? nameof(ResilienceActionHandlerAsync) : nameof(ResilienceActionHandler), exception.StackTrace);
        if (exception is DeferMessageAction defer)
            Assert.Equal(TimeSpan.FromMilliseconds(1234), defer.Delay);
    }

    [Theory]
    [MemberData(nameof(PolicyCases))]
    public async Task When_handling_an_application_error_should_keep_legacy_policy_behavior(
        bool isAsync, int policyCount, bool circuitBreaker)
    {
        //Arrange
        var exception = new InvalidOperationException("application failure");

        //Act
        var (failures, attempts) = await ExecuteAsync(isAsync, policyCount, circuitBreaker, exception);

        //Assert
        Assert.Same(exception, failures[0]);
        Assert.Equal(circuitBreaker ? 2 : (int)Math.Pow(4, policyCount), attempts);
        if (circuitBreaker)
            Assert.IsType<BrokenCircuitException>(failures[2]);
    }

    [Theory]
    [MemberData(nameof(PolicyCases))]
    public async Task When_handling_cancellation_should_keep_the_configured_legacy_policy_behavior(
        bool isAsync, int policyCount, bool circuitBreaker)
    {
        //Arrange
        var exception = new OperationCanceledException("cancelled");

        //Act
        var (failures, attempts) = await ExecuteAsync(isAsync, policyCount, circuitBreaker, exception);

        //Assert
        Assert.Same(exception, failures[0]);
        Assert.Equal(circuitBreaker ? 2 : (int)Math.Pow(4, policyCount), attempts);
        if (circuitBreaker)
            Assert.IsType<BrokenCircuitException>(failures[2]);
    }

    [Theory]
    [MemberData(nameof(PolicyCases))]
    public async Task When_handling_a_successful_request_should_return_it_without_retrying(
        bool isAsync, int policyCount, bool circuitBreaker)
    {
        //Act
        var (failures, attempts) = await ExecuteAsync(isAsync, policyCount, circuitBreaker, null);

        //Assert
        Assert.All(failures, Assert.Null);
        Assert.Equal(circuitBreaker ? 3 : 1, attempts);
    }

    private static async Task<(Exception?[] Failures, int Attempts)> ExecuteAsync(
        bool isAsync, int policyCount, bool circuitBreaker, Exception? exception)
    {
        var policies = new PolicyRegistry();
        var names = new List<string>();
        for (var i = 0; i < policyCount; i++)
        {
            var name = $"policy-{i}";
            names.Add(name);
            if (isAsync)
            {
                AsyncPolicy policy = circuitBreaker
                    ? Policy.Handle<Exception>().CircuitBreakerAsync(2, TimeSpan.FromMinutes(1))
                    : Policy.Handle<Exception>().RetryAsync(3);
                policies.Add(name, policy);
            }
            else
            {
                Policy policy = circuitBreaker
                    ? Policy.Handle<Exception>().CircuitBreaker(2, TimeSpan.FromMinutes(1))
                    : Policy.Handle<Exception>().Retry(3);
                policies.Add(name, policy);
            }
        }
#pragma warning disable CS0618 // Exercise the legacy policy registry.
        var context = new RequestContext { Policies = policies };
#pragma warning restore CS0618
        using var cancellation = new CancellationTokenSource();
        var failures = new Exception?[circuitBreaker ? 3 : 1];
        if (isAsync)
        {
            var request = new ResilienceActionCommandAsync(exception);
            var handler = new ExceptionPolicyHandlerAsync<ResilienceActionCommandAsync> { Context = context };
            handler.InitializeFromAttributeParams(names);
            handler.SetSuccessor(new ResilienceActionHandlerAsync { Context = context });
            for (var i = 0; i < failures.Length; i++)
                failures[i] = await Record.ExceptionAsync(async () =>
                    Assert.Same(request, await handler.HandleAsync(request, cancellation.Token)));
            Assert.Equal(cancellation.Token, request.CancellationToken);
            return (failures, request.Attempts);
        }
        else
        {
            var request = new ResilienceActionCommand(exception);
            var handler = new ExceptionPolicyHandler<ResilienceActionCommand> { Context = context };
            handler.InitializeFromAttributeParams(names);
            handler.SetSuccessor(new ResilienceActionHandler { Context = context });
            for (var i = 0; i < failures.Length; i++)
                failures[i] = Record.Exception(() => Assert.Same(request, handler.Handle(request)));
            return (failures, request.Attempts);
        }
    }
}
