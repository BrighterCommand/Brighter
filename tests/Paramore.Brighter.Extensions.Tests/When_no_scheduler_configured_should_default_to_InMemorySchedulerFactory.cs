#region Licence
/* The MIT License (MIT)

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
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class When_no_scheduler_configured_should_default_to_InMemorySchedulerFactory
{
    [Fact]
    public void Should_resolve_InMemorySchedulerFactory_as_default()
    {
        // Arrange — AddBrighter with no explicit UseScheduler or UseMessageScheduler
        var services = new ServiceCollection();
        services.AddBrighter();
        var provider = services.BuildServiceProvider();

        // Act
        var factory = provider.GetService<IAmAMessageSchedulerFactory>();

        // Assert — the default factory should be InMemorySchedulerFactory
        Assert.NotNull(factory);
        Assert.IsType<InMemorySchedulerFactory>(factory);
    }

    [Fact]
    public void Should_resolve_IAmAMessageScheduler_from_default_factory()
    {
        // Arrange — AddBrighter with no explicit UseScheduler
        var services = new ServiceCollection();
        services.AddBrighter();
        var provider = services.BuildServiceProvider();

        // Act
        var scheduler = provider.GetService<IAmAMessageScheduler>();

        // Assert — a scheduler should be resolvable from the default factory
        Assert.NotNull(scheduler);
        Assert.IsAssignableFrom<IAmAMessageScheduler>(scheduler);
    }

    [Fact]
    public void Should_resolve_IAmARequestSchedulerFactory_as_default()
    {
        // Arrange — AddBrighter with no explicit UseScheduler
        var services = new ServiceCollection();
        services.AddBrighter();
        var provider = services.BuildServiceProvider();

        // Act
        var factory = provider.GetService<IAmARequestSchedulerFactory>();

        // Assert — the default request scheduler factory should also be InMemorySchedulerFactory
        Assert.NotNull(factory);
        Assert.IsType<InMemorySchedulerFactory>(factory);
    }
}
