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

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;


namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-55 (FR-19, C-14, D16) - a consumer pipeline's ask always carries AlwaysNew (C-14), whatever flow
// started the Dispatcher that pumps it - even a flow as genuinely "live" as a controller action's own
// HTTP request, on which IHttpContextAccessor.HttpContext is non-null and names that very request's own
// DI scope. FR-23's stale-ambient guard does not reach this case at all, because nothing here is stale;
// this fact instead pins that the guarantee lives on the consumer pump's own flow (ADR 0075 step 4a,
// bracket 3, landed in T5.4), not on however the Dispatcher happened to be started.
public class DispatcherStartedFromRequestTests
{
    [Test]
    public async Task When_a_dispatcher_is_started_from_inside_a_request_the_consumer_should_not_adopt()
    {
        // Arrange - an opted-in host (JoinAmbient given as the extension's own argument), lifetime triple
        // {Scoped, Scoped, Scoped}, ten messages already on the bus before the request that starts the
        // pump is ever made
        await using var factory = new DispatcherFromRequestWebApplicationFactory();
        using var client = factory.CreateClient();

        for (var i = 0; i < DispatcherFromRequestWebApplicationFactory.MessageCount; i++)
        {
            var command = new DispatcherFromRequestCommand();
            factory.Producer.Send(new Message(
                new MessageHeader(command.Id, factory.RoutingKey, MessageType.MT_COMMAND),
                new MessageBody("{}")));
        }

        // Act - a controller action starts the Dispatcher itself, from inside this very request, and
        // blocks until all ten messages have been consumed
        var response = await client.PostAsync("/api/dispatcher-from-request", content: null);

        // Assert - the request succeeded, so every message really was consumed while it was still open
        response.EnsureSuccessStatusCode();

        var recorder = factory.Services.GetRequiredService<DispatcherFromRequestRecorder>();

        // Assert - the pump's flow genuinely carried the request's own, live ambient: every handler's own
        // IHttpContextAccessor observed the very HttpContext the controller was itself serving, not a
        // stale or absent one
        await Assert.That(recorder.ControllerHttpContext).IsNotNull();
        await Assert.That(recorder.ObservedHttpContexts.Count).IsEqualTo(DispatcherFromRequestWebApplicationFactory.MessageCount);
        foreach (var ctx in recorder.ObservedHttpContexts)
{
    await Assert.That(ctx).IsSameReferenceAs(recorder.ControllerHttpContext);
}

        // Assert - ten distinct, disposed mapper and handler instances - a consumer pipeline resolving
        // the ambient's own scope would instead have collapsed every message onto the one, shared,
        // still-open instance that scope's container caches (T5.4's own lesson)
        await Assert.That(recorder.Mappers.Distinct().Count()).IsEqualTo(DispatcherFromRequestWebApplicationFactory.MessageCount);
        await Assert.That(recorder.Handlers.Distinct().Count()).IsEqualTo(DispatcherFromRequestWebApplicationFactory.MessageCount);
        using (Assert.Multiple())
        {
            foreach (var mapper in recorder.Mappers)
            {
                await Assert.That(mapper.IsDisposed).IsTrue();
            }
        }
        using (Assert.Multiple())
        {
            foreach (var handler in recorder.Handlers)
            {
                await Assert.That(handler.IsDisposed).IsTrue();
            }
        }
        // Assert - every ask this pump made carried AlwaysNew, never JoinAmbient
        var scopeProviderRecorder = (DelegatingScopeProviderRecorder)factory.Services.GetRequiredService<IAmAScopeProvider>();
        await Assert.That(scopeProviderRecorder.Decisions).IsNotEmpty();
        using (Assert.Multiple())
        {
            foreach (var decision in scopeProviderRecorder.Decisions)
            {
                await Assert.That(decision).IsEqualTo(ScopeAffinity.AlwaysNew);
            }
        }
        // Assert - no Warning: FR-24's diagnostics are JoinAmbient-only, and this pump's ask never carried
        // that affinity
        await Assert.That(factory.LogEntries).DoesNotContain(entry => entry.Level >= LogLevel.Warning);
    }
}
