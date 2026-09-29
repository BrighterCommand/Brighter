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
using System.Linq;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Polly.Registry;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests;

// AC-51 (FR-13, FR-5, FR-6), ADR 0071 steps 2, 6 (the "most likely to fail" risk-mitigation test) —
// all three lifetimes Scoped, not opted in, and a handler factory whose Release throws
// InvalidOperationException. Every CommandProcessor here is built directly (not via AddBrighter), because
// AC-51's Given is specifically about a user-supplied IAmAHandlerFactorySync whose Release throws — a
// first-class extension point client code implements directly, per IAmAHandlerFactorySync's own doc
// comment — not about container-Scoped resolution, which ServiceProviderHandlerFactory.Release no longer
// participates in after T2.3 (it is a no-op; disposal is driven by the pipeline scope handle).
[System.Obsolete]
[NotInParallel(nameof(HandlerFactoryReleaseFailureTests))]
public class HandlerFactoryReleaseFailureTests
{
    [Test]
    public async System.Threading.Tasks.Task When_a_handler_factory_release_throws_it_should_be_logged_and_not_reach_the_caller()
    {
        // Arrange — a handler that completes normally; its factory's Release throws
        var recorder = new ReleaseOrderRecorder();
        var pipelineScope = new RecordingPipelineScope(recorder);
        var handler = new RecordingRequestHandler();
        var factory = new RecordingReleaseHandlerFactory(recorder, pipelineScope, _ => handler);
        factory.ThrowFor(handler);

        var registry = new SubscriberRegistry();
        registry.Register<HandlerReleaseThrowsCommand, RecordingRequestHandler>();

        var loggerProvider = new CapturingLoggerProvider();
        Initializer.Factory.AddProvider(loggerProvider);

        var commandProcessor = new CommandProcessor(
            registry, factory, new InMemoryRequestContextFactory(), new PolicyRegistry(),
            new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());

        // Act — the caller observes normal completion despite the release failure
        commandProcessor.Send(new HandlerReleaseThrowsCommand());

        // Assert — the handler ran to completion, and the release failure was logged at Error
        await Assert.That(handler.Completed).IsTrue();
        var releaseFailure = await Assert.That(loggerProvider.Entries.Where(e => e.EventId.Name == "FailedToReleaseHandler")).HasSingleItem();
        await Assert.That(releaseFailure.Level).IsEqualTo(LogLevel.Error);
    }

    [Test]
    public async System.Threading.Tasks.Task When_the_handler_itself_throws_the_release_failure_should_not_replace_it()
    {
        // Arrange — a handler whose Handle throws; its factory's Release also throws
        var recorder = new ReleaseOrderRecorder();
        var pipelineScope = new RecordingPipelineScope(recorder);
        var handler = new RecordingRequestHandler(throwOnHandle: true);
        var factory = new RecordingReleaseHandlerFactory(recorder, pipelineScope, _ => handler);
        factory.ThrowFor(handler);

        var registry = new SubscriberRegistry();
        registry.Register<HandlerReleaseThrowsCommand, RecordingRequestHandler>();

        var loggerProvider = new CapturingLoggerProvider();
        Initializer.Factory.AddProvider(loggerProvider);

        var commandProcessor = new CommandProcessor(
            registry, factory, new InMemoryRequestContextFactory(), new PolicyRegistry(),
            new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());

        // Act — the caller observes the handler's own exception, not an AggregateException composing it
        // with the release failure, and not the release failure itself
        var exception = await Assert.That(() => commandProcessor.Send(new HandlerReleaseThrowsCommand())).ThrowsExactly<InvalidOperationException>();
        await Assert.That(exception.Message).IsEqualTo("Handle failed.");

        // Assert — the release failure appears only in the log, at Error
        var releaseFailure = await Assert.That(loggerProvider.Entries.Where(e => e.EventId.Name == "FailedToReleaseHandler")).HasSingleItem();
        await Assert.That(releaseFailure.Level).IsEqualTo(LogLevel.Error);
    }

    [Test]
    public async System.Threading.Tasks.Task When_release_throws_for_one_of_three_tracked_handlers_the_others_are_still_released()
    {
        // Arrange — three tracked handlers, a factory whose Release throws for the first, and a
        // recording IAmAScope handle supplied by that factory's CreatePipelineScope()
        var recorder = new ReleaseOrderRecorder();
        var pipelineScope = new RecordingPipelineScope(recorder);
        var factory = new RecordingReleaseHandlerFactory(recorder, pipelineScope);

        var first = new RecordingHandler("First");
        var second = new RecordingHandler("Second");
        var third = new RecordingHandler("Third");
        factory.ThrowFor(first);

        var loggerProvider = new CapturingLoggerProvider();
        Initializer.Factory.AddProvider(loggerProvider);

        var lifetimeScope = new HandlerLifetimeScope(factory, pipelineScope);
        lifetimeScope.Add(first);
        lifetimeScope.Add(second);
        lifetimeScope.Add(third);

        // Act — the pipeline scope is released; the first handler's Release throws
        lifetimeScope.Dispose();

        // Assert — the other two were still released, and the scope was disposed last, after every release
        await Assert.That(recorder.Events).Contains("Released:First");
        await Assert.That(recorder.Events).Contains("Released:Second");
        await Assert.That(recorder.Events).Contains("Released:Third");
        await Assert.That(pipelineScope.WasDisposed).IsTrue();
        await Assert.That(recorder.Events.Last()).IsEqualTo("ScopeDisposed");

        // Assert — exactly one Error record names the failing release
        var releaseFailure = await Assert.That(loggerProvider.Entries.Where(e => e.EventId.Name == "FailedToReleaseHandler")).HasSingleItem();
        await Assert.That(releaseFailure.Level).IsEqualTo(LogLevel.Error);
        await Assert.That(releaseFailure.Message).Contains("First");
    }

    [Test]
    public async Task When_a_second_send_follows_a_release_failure_it_is_logged_and_swallowed_again()
    {
        // Arrange — the same host (one CommandProcessor, one handler factory) across two Sends; every
        // Release throws, regardless of which of the two handler instances it is called for
        var recorder = new ReleaseOrderRecorder();
        var pipelineScope = new RecordingPipelineScope(recorder);
        var factory = new RecordingReleaseHandlerFactory(recorder, pipelineScope, _ => new RecordingRequestHandler());
        factory.ThrowForEveryRelease();

        var registry = new SubscriberRegistry();
        registry.Register<HandlerReleaseThrowsCommand, RecordingRequestHandler>();

        var loggerProvider = new CapturingLoggerProvider();
        Initializer.Factory.AddProvider(loggerProvider);

        var commandProcessor = new CommandProcessor(
            registry, factory, new InMemoryRequestContextFactory(), new PolicyRegistry(),
            new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());

        // Act — first Send
        commandProcessor.Send(new HandlerReleaseThrowsCommand());

        // Act — second Send, in the same host
        commandProcessor.Send(new HandlerReleaseThrowsCommand());

        // Assert — both succeeded, and the failure was not latched: a second, separate Error was logged
        var releaseFailures = loggerProvider.Entries.Where(e => e.EventId.Name == "FailedToReleaseHandler").ToList();
        await Assert.That(releaseFailures.Count).IsEqualTo(2);
        using (Assert.Multiple())
        {
            foreach (var e in releaseFailures)
            {
                await Assert.That(e.Level).IsEqualTo(LogLevel.Error);
            }
        }
    }
}
