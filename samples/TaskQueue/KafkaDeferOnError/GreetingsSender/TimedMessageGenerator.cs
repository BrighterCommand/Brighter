#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using System.Threading.Tasks;
using Greetings.Ports.Commands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;

namespace GreetingsSender;

public class TimedMessageGenerator(IAmACommandProcessor processor, ILogger<TimedMessageGenerator> logger)
    : IHostedService, IDisposable
{
    private Timer? _timer;
    private long _iteration = 0;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Kafka Message Generator is starting");
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Kafka Message Generator is stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private void DoWork(object? state)
    {
        _iteration++;
        var greetingEvent = new GreetingEvent { Id = Id.Random(), Greeting = $"Hello # {_iteration}" };
        processor.Post(greetingEvent);
        logger.LogInformation("Sending message with id {Id} and greeting {Request}", greetingEvent.Id, greetingEvent.Greeting);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
