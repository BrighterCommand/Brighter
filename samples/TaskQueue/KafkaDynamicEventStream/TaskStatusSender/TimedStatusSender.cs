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

        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(3));
            
        DoWork(null);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Kafka Message Generator is stopping.");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }
        
    private void DoWork(object? state)
    {
        _iteration++;

        var correlationId = Id.Random();
        var dueAt = DateTimeOffset.UtcNow.AddDays(1);
        
        var taskCreated = new TaskCreated(Id.Random(), DateTimeOffset.UtcNow, dueAt, [DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(10)]);
        taskCreated.CorrelationId = correlationId;
        
        Console.WriteLine("{0} Sending task created with id {1} and dueAt {2} with correlationId {3}", _iteration, taskCreated.Id, taskCreated.DueAt, taskCreated.CorrelationId);
        processor.Post(taskCreated);
        
        var taskUpdated = new TaskUpdated(Id.Random(), TaskStatus.App.TaskStatus.InProgress, dueAt,[DateTimeOffset.UtcNow.AddMinutes(5)]);
        taskUpdated.CorrelationId = correlationId;
        
        Console.WriteLine("{0} Sending task updated with id {1} and status {2} with correlationId {3}", _iteration, taskUpdated.Id, taskUpdated.Status, taskUpdated.CorrelationId);
        processor.Post(taskUpdated);
        
        var taskCompleted = new TaskUpdated(Id.Random(), TaskStatus.App.TaskStatus.Completed, dueAt, []);
        taskCompleted.CorrelationId = correlationId;
        
        Console.WriteLine("{0} Sending task completed with id {1} and status {2} and correlationId {3}", _iteration, taskCompleted.Id, taskCompleted.Status, taskCompleted.CorrelationId);
        processor.Post(taskCompleted);

    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
