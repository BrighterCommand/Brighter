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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// AC-33 (FR-13), ADR 0071 steps 2, 6 (second required test) — all three lifetimes Scoped, not opted in
// to ambient scope, so the pipeline scope is one Brighter created and owns: a borrowed scope cannot be
// used (Brighter never disposes one, FR-12) and the provider supplies no disposable scope of its own
// (D17), which is why this discharges FR-13 and not FR-24. IPoisonedDependency is resolved through a
// real container-Scoped registration, so disposing the handler pipeline's owned scope disposes the
// container's IServiceScope, which throws from IPoisonedDependency's own Dispose().
public class SuccessfulSendPipelineScopeDisposalLoggingTests
{
    [Fact]
    public void When_a_successful_send_pipeline_scope_disposal_throws_the_result_should_be_unchanged()
    {
        // Arrange — a handler that completes normally; its Scoped dependency's Dispose() throws when
        // the pipeline's owned scope is released
        var recorder = new HandlerCompletionRecorder();
        var services = new ServiceCollection();
        services.AddScoped<IPoisonedDependency, PoisonedDependency>();
        services.AddSingleton(recorder);
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        var commandProcessor = services.BuildServiceProvider().GetRequiredService<IAmACommandProcessor>();

        var loggerProvider = new CapturingLoggerProvider();
        Initializer.Factory.AddProvider(loggerProvider);

        // Act — the first Send completes despite its owned pipeline scope's disposal throwing
        commandProcessor.Send(new PoisonedScopeCompletingHandlerCommand());

        // Assert — the handler ran to completion, unaffected by the disposal failure, and the failure
        // was logged at Error. Filtered by CategoryName as well as EventId.Name: HandlerLifetimeScope and
        // TransformPipelineDrain both log a "FailedToDisposePipelineScope" event, and xUnit may run this
        // test concurrently with one that exercises the other, against the same shared static logger
        // factory (Initializer.Factory) — CategoryName is what tells them apart.
        Assert.Equal(1, recorder.Completions);
        var disposalFailure = Assert.Single(loggerProvider.Entries.Where(IsHandlerScopeDisposalFailure));
        Assert.Equal(LogLevel.Error, disposalFailure.Level);

        // Act — a second Send in the same host
        commandProcessor.Send(new PoisonedScopeCompletingHandlerCommand());

        // Assert — not latched: the handler completed again and a second, separate Error was logged
        Assert.Equal(2, recorder.Completions);
        var disposalFailures = loggerProvider.Entries.Where(IsHandlerScopeDisposalFailure).ToList();
        Assert.Equal(2, disposalFailures.Count);
        Assert.All(disposalFailures, e => Assert.Equal(LogLevel.Error, e.Level));
    }

    private static bool IsHandlerScopeDisposalFailure(CapturedLogEntry entry) =>
        entry.EventId.Name == "FailedToDisposePipelineScope" &&
        entry.CategoryName == "Paramore.Brighter.HandlerLifetimeScope";
}
