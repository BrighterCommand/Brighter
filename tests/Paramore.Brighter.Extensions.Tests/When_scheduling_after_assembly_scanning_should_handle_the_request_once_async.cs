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

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ScannedSchedulerRequestTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_scheduling_after_assembly_scanning_should_handle_the_request_once_async(bool addConsumers)
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var handler = new ScannedSchedulerCommandHandler();
        var services = new ServiceCollection();
        services.AddSingleton(handler);
        var builder = addConsumers ? services.AddConsumers() : services.AddBrighter();
        builder.UseScheduler(new InMemorySchedulerFactory { TimeProvider = timeProvider })
            .AutoFromAssemblies();
        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var request = new ScannedSchedulerCommand { Greeting = "Hello" };

        //Act
        await processor.SendAsync(TimeSpan.FromSeconds(10), request);
        Assert.Empty(handler.Received);
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        //Assert
        var received = Assert.Single(handler.Received);
        Assert.Equal(request.Id, received.Id);
        Assert.Equal(request.Greeting, received.Greeting);
        timeProvider.Advance(TimeSpan.FromSeconds(10));
        Assert.Single(handler.Received);
    }
}
