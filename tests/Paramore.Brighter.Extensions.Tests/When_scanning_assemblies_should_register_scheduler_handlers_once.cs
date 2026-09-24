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

using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class SchedulerHandlerRegistrationTests
{
    public static TheoryData<bool, bool, bool, int> RegistrationCases
    {
        get
        {
            var cases = new TheoryData<bool, bool, bool, int>();
            foreach (var addConsumers in new[] { false, true })
            {
                foreach (var explicitScheduler in new[] { false, true })
                {
                    foreach (var explicitAssembly in new[] { false, true })
                    {
                        foreach (var scanCount in new[] { 0, 1, 2 })
                            cases.Add(addConsumers, explicitScheduler, explicitAssembly, scanCount);
                    }
                }
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(RegistrationCases))]
    public void When_scanning_assemblies_should_register_scheduler_handlers_once(
        bool addConsumers, bool explicitScheduler, bool explicitAssembly, int scanCount)
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = addConsumers ? services.AddConsumers() : services.AddBrighter();
        if (explicitScheduler)
            builder.UseScheduler(new InMemorySchedulerFactory());

        //Act
        for (var scan = 0; scan < scanCount; scan++)
            builder.AutoFromAssemblies(explicitAssembly ? [typeof(IBrighterBuilder).Assembly] : null);

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ServiceCollectionSubscriberRegistry>();

        //Assert
        Assert.Equal(typeof(FireSchedulerRequestHandler),
            Assert.Single(registry.Get(new FireSchedulerRequest(), new RequestContext())));
        Assert.Equal(typeof(FireSchedulerMessageHandler),
            Assert.Single(registry.Get(new FireSchedulerMessage(), new RequestContext())));
    }
}
