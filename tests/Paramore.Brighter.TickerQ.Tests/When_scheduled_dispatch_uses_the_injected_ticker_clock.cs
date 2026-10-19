#region Licence
/* The MIT License (MIT)
Copyright © 2026 Jay Sen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageScheduler.TickerQ;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.TickerQ.Tests.TestDoubles;
using Polly.Registry;
using TickerQ.DependencyInjection;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces;
using TickerQ.Utilities.Interfaces.Managers;

namespace Paramore.Brighter.TickerQ.Tests;

[Collection("Scheduler")]
public class TickerQInjectedClockTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task When_scheduled_dispatch_uses_the_injected_ticker_clock(bool message, bool cancel)
    {
        //Arrange
        var clock = new InMemoryTickerClock();
        var dispatch = new TickerQClockDispatchState { Message = message };
        var subscribers = new SubscriberRegistry();
        subscribers.RegisterAsync<FireSchedulerMessage, InMemoryScheduledDispatchHandler<FireSchedulerMessage>>();
        subscribers.RegisterAsync<FireSchedulerRequest, InMemoryScheduledDispatchHandler<FireSchedulerRequest>>();
        var processor = new CommandProcessor(subscribers,
            new SimpleHandlerFactoryAsync(type =>
                (IHandleRequestsAsync)Activator.CreateInstance(type, dispatch.Delivered)!),
            new InMemoryRequestContextFactory(), new PolicyRegistry(),
            new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTickerQ();
        services.AddSingleton(clock);
        services.Replace(ServiceDescriptor.Singleton<ITickerClock>(clock));
        services.AddSingleton(dispatch);
        services.AddSingleton<IAmACommandProcessor>(processor);
        services.AddTransient<BrighterTickerQSchedulerJob>();
        await using var provider = services.BuildServiceProvider();
        TickerQModuleInitializer.EnsureOneTimeSetupTickerQ();
        IRequest request = message ? new FireSchedulerMessage() : new FireSchedulerRequest();
        var ticker = new TimeTickerEntity
        {
            Id = Guid.NewGuid(),
            ExecutionTime = clock.UtcNow.AddSeconds(-2),
            Function = nameof(TickerQClockDispatch.DispatchWithCustomClock),
            Request = TickerHelper.CreateTickerRequest(JsonSerializer.Serialize(request, request.GetType(),
                JsonSerialisationOptions.Options))
        };
        var manager = provider.GetRequiredService<ITimeTickerManager<TimeTickerEntity>>();
        Assert.True((await manager.AddAsync(ticker)).IsSucceeded);
        var host = provider.GetRequiredService<ITickerQHostScheduler>();

        //Act
        await host.StartAsync();
        try
        {
            var invocation = await dispatch.Invocation.Task.WaitAsync(TimeSpan.FromSeconds(10));

            //Assert
            Assert.False(invocation.Dispatch.IsCompleted);
            await clock.WaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Empty(dispatch.Delivered);
            if (cancel)
            {
                invocation.Context.RequestCancellation();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    invocation.Dispatch.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.Empty(dispatch.Delivered);
            }
            else
            {
                clock.SetUtcNow(invocation.Context.ScheduledFor);
                await invocation.Dispatch.WaitAsync(TimeSpan.FromSeconds(5));
                var delivered = Assert.Single(dispatch.Delivered);
                Assert.Equal(request.GetType(), delivered.GetType());
                Assert.Equal(request.Id, delivered.Id);
            }
        }
        finally
        {
            await host.StopAsync();
            await manager.DeleteAsync(ticker.Id);
        }
    }
}
