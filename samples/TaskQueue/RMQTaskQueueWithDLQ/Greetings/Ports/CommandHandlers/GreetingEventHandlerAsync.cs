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
using Paramore.Brighter;
using Paramore.Brighter.Reject.Attributes;

namespace Greetings.Ports.CommandHandlers;

public class GreetingEventHandlerAsync : RequestHandlerAsync<GreetingEvent>
{
    private static int _messageCount = 0;

    [RejectMessageOnErrorAsync(step: 0)]
    public override async Task<GreetingEvent> HandleAsync(GreetingEvent @event, CancellationToken cancellationToken = default)
    {
        var count = Interlocked.Increment(ref _messageCount);
        Console.WriteLine($"Received message #{count}: {@event.Greeting}");

        // Reject every 5th message to demonstrate DLQ behavior
        if (count % 5 == 0)
        {
            Console.WriteLine($"  -> Simulating failure for message #{count}");
            throw new InvalidOperationException($"Simulated failure for message #{count}");
        }

        Console.WriteLine($"  -> Successfully processed message #{count}");
        return await base.HandleAsync(@event, cancellationToken);
    }
}
