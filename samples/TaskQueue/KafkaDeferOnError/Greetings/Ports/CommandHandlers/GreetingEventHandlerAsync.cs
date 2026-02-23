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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Ports.Commands;
using Paramore.Brighter;
using Paramore.Brighter.Actions;

namespace Greetings.Ports.CommandHandlers;

/// <summary>
/// Demonstrates DeferMessageAction by throwing it directly from the handler.
/// A DeferMessageOnErrorAttribute does not exist yet, so we throw DeferMessageAction
/// manually to trigger the requeue behavior. Every 3rd message is deferred on its first
/// two attempts and succeeds on the 3rd attempt, showing eventual success after retry.
/// </summary>
public class GreetingEventHandlerAsync : RequestHandlerAsync<GreetingEvent>
{
    private static int _messageCount = 0;
    private static readonly ConcurrentDictionary<string, int> s_retryTracker = new();

    private const int DEFER_EVERY_NTH = 3;
    private const int MAX_DEFERRALS = 2;

    public override async Task<GreetingEvent> HandleAsync(GreetingEvent @event, CancellationToken cancellationToken = default)
    {
        var count = Interlocked.Increment(ref _messageCount);
        var messageId = @event.Id.ToString();
        var retryCount = s_retryTracker.AddOrUpdate(messageId, 0, (_, existing) => existing + 1);

        Console.WriteLine($"Received message #{count}: {@event.Greeting} (message ID: {messageId}, attempt: {retryCount + 1})");

        // Every 3rd message is a "fail" message that gets deferred
        if (count % DEFER_EVERY_NTH == 0 && retryCount < MAX_DEFERRALS)
        {
            Console.WriteLine($"  -> Deferring message #{count} (attempt {retryCount + 1} of {MAX_DEFERRALS + 1})");
            throw new DeferMessageAction();
        }

        // Clean up retry tracker for completed messages
        s_retryTracker.TryRemove(messageId, out _);

        if (retryCount >= MAX_DEFERRALS)
        {
            Console.WriteLine($"  -> Message #{count} succeeded after {retryCount + 1} attempts");
        }
        else
        {
            Console.WriteLine($"  -> Successfully processed message #{count}");
        }

        return await base.HandleAsync(@event, cancellationToken);
    }
}
