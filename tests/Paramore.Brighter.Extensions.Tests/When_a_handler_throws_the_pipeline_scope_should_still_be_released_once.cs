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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// AC-7 (FR-6), ADR 0071 step 2 — not opted in to ambient scope (no IAmAScopeProvider registered, which
// does not exist yet at this point in the spec anyway), lifetime triple {Scoped, Scoped, Scoped}. Both
// clauses only hold if the pipeline scope is torn down through the same path whether the handler
// completes or throws: an implementation that only disposed on the happy path would leak the dependency,
// or one that wrapped the failure would replace the exception the caller observes.
public class HandlerThrowsPipelineScopeReleasedOnceTests
{
    [Fact]
    public async Task When_a_handler_throws_the_pipeline_scope_should_still_be_released_once()
    {
        // Arrange
        var recorder = new CountingDisposableRecorder();
        var services = new ServiceCollection();
        services.AddScoped<CountingDisposable>();
        services.AddSingleton(recorder);
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        var commandProcessor = services.BuildServiceProvider().GetRequiredService<IAmACommandProcessor>();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => commandProcessor.SendAsync(new ThrowingHandlerCommand()));

        // Assert — the caller observes the handler's own exception, unchanged
        Assert.Equal(ThrowingHandlerCommandHandlerAsync.FailureMessage, exception.Message);

        // Assert — the Scoped dependency the handler resolved was disposed exactly once
        var dependency = Assert.Single(recorder.Instances);
        Assert.Equal(1, dependency.DisposeCount);
    }
}
