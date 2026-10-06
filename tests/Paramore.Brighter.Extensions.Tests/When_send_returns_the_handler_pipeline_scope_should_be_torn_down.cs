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

using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// AC-9 (FR-7, FR-11) — not opted in to ambient scope (no IAmAScopeProvider registered, which does not
// exist yet at this point in the spec anyway), lifetime triple {Scoped, Scoped, Scoped}. Both clauses
// below only hold if Send owns a real IServiceScope for the handler pipeline: an implementation that
// resolves the Scoped dependency from the root provider would leave it undisposed after Send returns,
// and would hand a second Send the same cached instance.
public class SendHandlerPipelineScopeTeardownTests
{
    [Fact]
    public void When_send_returns_the_handler_pipeline_scope_should_be_torn_down()
    {
        // Arrange
        var recorder = new HandlerMarkerRecorder();
        var services = new ServiceCollection();
        services.AddScoped<IMarker, Marker>();
        services.AddSingleton(recorder);
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        var commandProcessor = services.BuildServiceProvider().GetRequiredService<IAmACommandProcessor>();

        // Act — first Send
        commandProcessor.Send(new ScopedHandlerCommand());

        // Assert — by the time Send returns, the Scoped dependency it resolved has already been disposed
        var firstMarker = Assert.Single(recorder.Markers);
        Assert.True(firstMarker.IsDisposed);

        // Act — second Send
        commandProcessor.Send(new ScopedHandlerCommand());

        // Assert — the second Send resolved a different instance, not the first Send's disposed one
        Assert.Equal(2, recorder.Markers.Count);
        Assert.NotSame(firstMarker, recorder.Markers[1]);
    }
}
