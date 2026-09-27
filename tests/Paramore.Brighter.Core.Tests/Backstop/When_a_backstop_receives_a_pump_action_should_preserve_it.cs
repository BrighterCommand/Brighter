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

    public static TheoryData<bool, string, string> ActionCases
    {
        get
        {
            var cases = new TheoryData<bool, string, string>();
            foreach (var isAsync in new[] { false, true })
                foreach (var backstop in new[] { "defer", "reject", "dont-ack" })
                    foreach (var action in new[] { "reject", "defer", "dont-ack", "invalid", "cancel", "task-cancel" })
                        cases.Add(isAsync, backstop, action);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ActionCases))]
    public async Task When_a_backstop_receives_a_pump_action_should_preserve_it(bool isAsync, string backstop, string action)
    {
        //Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Exception original = action switch
        {
            "reject" => new RejectMessageAction("permanent failure"),
            "defer" => new DeferMessageAction("try later", new InvalidOperationException("reason"), 1234),
            "dont-ack" => new DontAckAction("leave unacknowledged"),
            "invalid" => new InvalidMessageAction("invalid payload"),
            "cancel" => new OperationCanceledException("shutdown", cancellation.Token),
            "task-cancel" => new TaskCanceledException("shutdown", null, cancellation.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        //Act
        var thrown = await Record.ExceptionAsync(() => ExecuteAsync(isAsync, backstop, original, cancellation.Token));

        //Assert
        Assert.Same(original, thrown);
        Assert.Contains(isAsync ? nameof(BackstopActionHandlerAsync) : nameof(BackstopActionHandler), thrown!.StackTrace);
        if (thrown is DeferMessageAction defer)
            Assert.Equal(TimeSpan.FromMilliseconds(1234), defer.Delay);
        if (thrown is OperationCanceledException cancelled)
            Assert.Equal(cancellation.Token, cancelled.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(BackstopCases))]
    public async Task When_a_backstop_receives_an_application_error_should_wrap_it(bool isAsync, string backstop)
    {
        //Arrange
        var original = new InvalidOperationException("application failure");

        //Act
        var thrown = await Record.ExceptionAsync(() => ExecuteAsync(isAsync, backstop, original));

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
