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

namespace DlqConsole;

public class DlqGreetingEventHandlerAsync : RequestHandlerAsync<GreetingEvent>
{
    public override async Task<GreetingEvent> HandleAsync(GreetingEvent @event, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("=== DEAD LETTER MESSAGE ===");
        Console.WriteLine($"  Message ID: {@event.Id}");
        Console.WriteLine($"  Greeting: {@event.Greeting}");

        if (Context?.Bag != null)
        {
            if (Context.Bag.TryGetValue("OriginalTopic", out var originalTopic))
            {
                Console.WriteLine($"  Original Topic: {originalTopic}");
            }
            if (Context.Bag.TryGetValue("RejectionReason", out var rejectionReason))
            {
                Console.WriteLine($"  Rejection Reason: {rejectionReason}");
            }
        }

        Console.WriteLine("===========================");
        return await base.HandleAsync(@event, cancellationToken);
    }
}
