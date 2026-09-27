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
using Paramore.Brighter.Core.Tests.Backstop.TestDoubles;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Defer.Handlers;
using Paramore.Brighter.DontAck.Handlers;
using Paramore.Brighter.Reject.Handlers;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Backstop;

public class BackstopPumpActionTests
{
    public static TheoryData<bool, string> BackstopCases
    {
        get
        {
            var cases = new TheoryData<bool, string>();
            foreach (var isAsync in new[] { false, true })
                foreach (var backstop in new[] { "defer", "reject", "dont-ack" })
                    cases.Add(isAsync, backstop);
            return cases;
        }
    }

    public static TheoryData<bool, string, string, bool> ActionCases
    {
        get
        {
            var cases = new TheoryData<bool, string, string, bool>();
            foreach (var isAsync in new[] { false, true })
                foreach (var backstop in new[] { "defer", "reject", "dont-ack" })
                    foreach (var action in new[] { "reject", "defer", "dont-ack", "invalid" })
                        foreach (var aggregate in new[] { false, true })
                            cases.Add(isAsync, backstop, action, aggregate);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ActionCases))]
    public async Task When_a_backstop_receives_a_pump_action_should_preserve_it(bool isAsync, string backstop, string action, bool aggregate)
    {
        //Arrange
        Exception pumpAction = action switch
        {
            "reject" => new RejectMessageAction("permanent failure"),
            "defer" => new DeferMessageAction("try later", new InvalidOperationException("reason"), 1234),
            "dont-ack" => new DontAckAction("leave unacknowledged"),
            "invalid" => new InvalidMessageAction("invalid payload"),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        var original = aggregate ? new AggregateException(pumpAction) : pumpAction;

        //Act
        var thrown = await Record.ExceptionAsync(() => ExecuteAsync(isAsync, backstop, original));

        //Assert
        Assert.Same(original, thrown);
        Assert.Contains(isAsync ? nameof(BackstopActionHandlerAsync) : nameof(BackstopActionHandler), thrown!.StackTrace);
        if (pumpAction is DeferMessageAction defer)
            Assert.Equal(TimeSpan.FromMilliseconds(1234), defer.Delay);
    }

    public static TheoryData<bool, string, string> ApplicationErrorCases
    {
        get
        {
            var cases = new TheoryData<bool, string, string>();
            foreach (var isAsync in new[] { false, true })
                foreach (var backstop in new[] { "defer", "reject", "dont-ack" })
                    foreach (var error in new[]
                    {
                        "application", "cancel", "task-cancel", "caller-cancel", "caller-task-cancel",
                        "aggregate", "mixed-aggregate", "empty-aggregate", "nested-aggregate"
                    })
                        cases.Add(isAsync, backstop, error);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ApplicationErrorCases))]
    public async Task When_a_backstop_receives_an_application_error_should_wrap_it(bool isAsync, string backstop, string error)
    {
        //Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Exception original = error switch
        {
            "application" => new InvalidOperationException("application failure"),
            "cancel" or "caller-cancel" => new OperationCanceledException("operation timed out", cancellation.Token),
            "task-cancel" or "caller-task-cancel" => new TaskCanceledException("operation timed out", null, cancellation.Token),
            "aggregate" => new AggregateException(new InvalidOperationException("application failure")),
            "mixed-aggregate" => new AggregateException(new RejectMessageAction("reject"), new InvalidOperationException("application failure")),
            "empty-aggregate" => new AggregateException(),
            "nested-aggregate" => new AggregateException(new AggregateException(new RejectMessageAction("reject"))),
            _ => throw new ArgumentOutOfRangeException(nameof(error))
        };
        var callerToken = error is "caller-cancel" or "caller-task-cancel" ? cancellation.Token : default;

        //Act
        var thrown = await Record.ExceptionAsync(() => ExecuteAsync(isAsync, backstop, original, callerToken));

        //Assert
        Assert.NotNull(thrown);
        Assert.Same(original, thrown.InnerException);
        Assert.Equal(original.Message, thrown.Message);
        switch (backstop)
        {
            case "defer":
                Assert.Equal(TimeSpan.FromMilliseconds(5000), Assert.IsType<DeferMessageAction>(thrown).Delay);
                break;
            case "reject":
                Assert.IsType<RejectMessageAction>(thrown);
                break;
            case "dont-ack":
                Assert.IsType<DontAckAction>(thrown);
                break;
        }
    }

    [Theory]
    [MemberData(nameof(BackstopCases))]
    public async Task When_a_backstop_receives_multiple_pump_actions_should_preserve_the_aggregate(bool isAsync, string backstop)
    {
        //Arrange
        var original = new AggregateException(
            new RejectMessageAction("reject"),
            new DeferMessageAction("defer", new InvalidOperationException("reason"), 1234),
            new DontAckAction("nack"),
            new InvalidMessageAction("invalid"));

        //Act
        var thrown = await Record.ExceptionAsync(() => ExecuteAsync(isAsync, backstop, original));

        //Assert
        Assert.Same(original, thrown);
        Assert.Contains(isAsync ? nameof(BackstopActionHandlerAsync) : nameof(BackstopActionHandler), thrown!.StackTrace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_backstops_are_stacked_should_preserve_the_innermost_action(bool isAsync)
    {
        //Arrange
        var original = new InvalidOperationException("application failure");
        Exception? thrown;
        if (isAsync)
        {
            var command = new BackstopActionCommandAsync(original);
            var inner = new DeferMessageOnErrorHandlerAsync<BackstopActionCommandAsync>();
            inner.InitializeFromAttributeParams(1234);
            inner.SetSuccessor(new BackstopActionHandlerAsync());
            var outer = new RejectMessageOnErrorHandlerAsync<BackstopActionCommandAsync>();
            outer.SetSuccessor(inner);

            //Act
            thrown = await Record.ExceptionAsync(() => outer.HandleAsync(command));
        }
        else
        {
            var command = new BackstopActionCommand(original);
            var inner = new DeferMessageOnErrorHandler<BackstopActionCommand>();
            inner.InitializeFromAttributeParams(1234);
            inner.SetSuccessor(new BackstopActionHandler());
            var outer = new RejectMessageOnErrorHandler<BackstopActionCommand>();
            outer.SetSuccessor(inner);

            //Act
            thrown = Record.Exception(() => outer.Handle(command));
        }

        //Assert
        var action = Assert.IsType<DeferMessageAction>(thrown);
        Assert.Same(original, action.InnerException);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), action.Delay);
    }

    [Theory]
    [MemberData(nameof(BackstopCases))]
    public async Task When_a_backstop_receives_a_successful_request_should_return_it(bool isAsync, string backstop)
    {
        //Arrange / Act
        var thrown = await Record.ExceptionAsync(() => ExecuteAsync(isAsync, backstop));

        //Assert
        Assert.Null(thrown);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_sending_a_command_with_a_defer_backstop_should_preserve_an_explicit_rejection(bool isAsync)
    {
        //Arrange
        var original = new RejectMessageAction("permanent failure");
        var registry = new SubscriberRegistry();
        using var pipelines = new ResiliencePipelineRegistry<string>();
        Exception? thrown;
        if (isAsync)
        {
            var command = new BackstopActionCommandAsync(original);
            registry.RegisterAsync<BackstopActionCommandAsync, BackstopActionHandlerAsync>();
            var factory = new SimpleHandlerFactoryAsync(type =>
            {
                if (type == typeof(BackstopActionHandlerAsync))
                    return new BackstopActionHandlerAsync();
                if (type == typeof(DeferMessageOnErrorHandlerAsync<BackstopActionCommandAsync>))
                    return new DeferMessageOnErrorHandlerAsync<BackstopActionCommandAsync>();
                throw new ArgumentOutOfRangeException(nameof(type));
            });
            var processor = new CommandProcessor(registry, factory, new InMemoryRequestContextFactory(),
                new PolicyRegistry(), pipelines, new InMemorySchedulerFactory());

            //Act
            thrown = await Record.ExceptionAsync(() => processor.SendAsync(command));

            //Assert
            Assert.Equal(1, command.Attempts);
        }
        else
        {
            var command = new BackstopActionCommand(original);
            registry.Register<BackstopActionCommand, BackstopActionHandler>();
            var factory = new SimpleHandlerFactorySync(type =>
            {
                if (type == typeof(BackstopActionHandler))
                    return new BackstopActionHandler();
                if (type == typeof(DeferMessageOnErrorHandler<BackstopActionCommand>))
                    return new DeferMessageOnErrorHandler<BackstopActionCommand>();
                throw new ArgumentOutOfRangeException(nameof(type));
            });
            var processor = new CommandProcessor(registry, factory, new InMemoryRequestContextFactory(),
                new PolicyRegistry(), pipelines, new InMemorySchedulerFactory());

            //Act
            thrown = Record.Exception(() => processor.Send(command));

            //Assert
            Assert.Equal(1, command.Attempts);
        }
        Assert.Same(original, thrown);
    }

    private static async Task ExecuteAsync(bool isAsync, string backstop, Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        if (isAsync)
        {
            var command = new BackstopActionCommandAsync(exception);
            RequestHandlerAsync<BackstopActionCommandAsync> handler = backstop switch
            {
                "defer" => new DeferMessageOnErrorHandlerAsync<BackstopActionCommandAsync>(),
                "reject" => new RejectMessageOnErrorHandlerAsync<BackstopActionCommandAsync>(),
                "dont-ack" => new DontAckOnErrorHandlerAsync<BackstopActionCommandAsync>(),
                _ => throw new ArgumentOutOfRangeException(nameof(backstop))
            };
            if (backstop == "defer")
                handler.InitializeFromAttributeParams(5000);
            handler.SetSuccessor(new BackstopActionHandlerAsync());
            try
            {
                Assert.Same(command, await handler.HandleAsync(command, cancellationToken));
            }
            finally
            {
                Assert.Equal(1, command.Attempts);
                Assert.Equal(cancellationToken, command.CancellationToken);
            }
        }
        else
        {
            var command = new BackstopActionCommand(exception);
            RequestHandler<BackstopActionCommand> handler = backstop switch
            {
                "defer" => new DeferMessageOnErrorHandler<BackstopActionCommand>(),
                "reject" => new RejectMessageOnErrorHandler<BackstopActionCommand>(),
                "dont-ack" => new DontAckOnErrorHandler<BackstopActionCommand>(),
                _ => throw new ArgumentOutOfRangeException(nameof(backstop))
            };
            if (backstop == "defer")
                handler.InitializeFromAttributeParams(5000);
            handler.SetSuccessor(new BackstopActionHandler());
            try
            {
                Assert.Same(command, handler.Handle(command));
            }
            finally
            {
                Assert.Equal(1, command.Attempts);
            }
        }
    }
}
