#region Licence

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using TaskStatus.Driving_Ports;
using TaskStatus.Ports;

namespace TaskStatusSender;

public class TimedStatusSender(IAmACommandProcessor processor, ILogger<TimedStatusSender> logger) : IHostedService, IDisposable
{
    private Timer? _timer;
    private long _iteration = 0;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Kafka Message Generator is starting.");

        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
            
        DoWork(null);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Kafka Message Generator is stopping.");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }
        
    private async void DoWork(object? state)
    {
        _iteration++;

        var id = Id.Random;
        var dueAt = DateTimeOffset.UtcNow.AddDays(1);
        
        var taskCreated = new TaskCreated(id, DateTimeOffset.UtcNow, dueAt, [DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(10)]);
        
        logger.LogInformation("{Iteration} Sending task created with id {Id} and dueAt {DueAt}", _iteration, taskCreated.Id, taskCreated.DueAt);
        await processor.PostAsync(taskCreated);
        
        await Task.Delay(1000); // Simulate some processing delay
        
        var taskUpdated = new TaskUpdated(id, TaskStatus.App.TaskStatus.InProgress, dueAt,[DateTimeOffset.UtcNow.AddMinutes(5)]);
        
        logger.LogInformation("{Iteration} Sending task updated with id {Id} and status {Status}", _iteration, taskUpdated.Id, taskUpdated.Status);
        await processor.PostAsync(taskUpdated);
        
        await Task.Delay(1000); // Simulate some processing delay

        var taskCompleted = new TaskUpdated(id, TaskStatus.App.TaskStatus.Completed, dueAt, []);
        
        logger.LogInformation("{Iteration} Sending task completed with id {Id} and status {Status}", _iteration, taskCompleted.Id, taskCompleted.Status);
        await processor.PostAsync(taskCompleted);

    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
