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

using Hangfire;
using Hangfire.InMemory;
using Hangfire.Logging;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Hangfire.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.Hangfire;

namespace Paramore.Brighter.Hangfire.Tests;

[Collection("Scheduler")]
public class HangfireSchedulerFactoryConfigurationTests : IDisposable
{
    private readonly JobStorage? _previousStorage;
    private readonly JobActivator _previousActivator = JobActivator.Current;

    public HangfireSchedulerFactoryConfigurationTests()
    {
        try
        {
            _previousStorage = JobStorage.Current;
        }
        catch (InvalidOperationException)
        {
            _previousStorage = null;
        }

        JobStorage.Current = null!;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_registering_a_scheduler_before_storage_initialization_should_schedule_after_configuration(
        bool supplyClient, bool isAsync)
    {
        //Arrange
        using var storage = new InMemoryStorage();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHangfire(configuration => configuration.UseStorage(storage));
        var factory = supplyClient
            ? new HangfireMessageSchedulerFactory { Client = new BackgroundJobClient(storage) }
            : new HangfireMessageSchedulerFactory();
        services.AddBrighter().UseScheduler(factory);
        Assert.Throws<InvalidOperationException>(() => JobStorage.Current);
        using var provider = services.BuildServiceProvider();
        if (!supplyClient)
            provider.GetRequiredService<IBackgroundJobClientV2>();

        var delay = TimeSpan.FromMinutes(5);
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("factory-configuration"), MessageType.MT_EVENT),
            new MessageBody("test"));

        //Act
        string requestId;
        string messageId;
        if (isAsync)
        {
            requestId = await provider.GetRequiredService<IAmARequestSchedulerAsync>()
                .ScheduleAsync(new MyEvent(), RequestSchedulerType.Publish, delay);
            messageId = await provider.GetRequiredService<IAmAMessageSchedulerAsync>()
                .ScheduleAsync(message, delay);
        }
        else
        {
            requestId = provider.GetRequiredService<IAmARequestSchedulerSync>()
                .Schedule(new MyEvent(), RequestSchedulerType.Publish, delay);
            messageId = provider.GetRequiredService<IAmAMessageSchedulerSync>()
                .Schedule(message, delay);
        }

        //Assert
        Assert.Equal(2, storage.GetMonitoringApi().ScheduledCount());
        using var connection = storage.GetConnection();
        Assert.Equal("Scheduled", connection.GetJobData(requestId).State);
        Assert.Equal("Scheduled", connection.GetJobData(messageId).State);
        if (supplyClient)
            Assert.Throws<InvalidOperationException>(() => JobStorage.Current);
    }

    public void Dispose()
    {
        JobStorage.Current = _previousStorage!;
        JobActivator.Current = _previousActivator;
        LogProvider.SetCurrentLogProvider(null!);
    }
}
