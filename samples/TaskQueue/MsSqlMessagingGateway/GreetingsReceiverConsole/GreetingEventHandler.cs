#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Events.Ports.Commands;
using Paramore.Brighter;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Attributes;

namespace GreetingsReceiverConsole
{
    public class GreetingEventHandler : RequestHandler<GreetingEvent>
    {
        // The Inbox makes this handler idempotent: a redelivered message is recognised rather
        // than reprocessed. OnceOnlyAction.Warn logs the duplicate and drops it without calling
        // this method; Throw is what you want when the caller needs to know one arrived.
        //
        // This is the ATTRIBUTE route rather than AddConsumers' global InboxConfiguration.
        // AddEventBus hands that configuration to the pipeline only on its ExternalBus arms;
        // with no producers registered it calls NoExternalBus() instead, and that overload
        // takes no inbox. This receiver has no producers, so the global configuration would be
        // silently ignored.
        //
        // The handler lives HERE rather than in the shared Events project because the other
        // three apps in this sample also call AutoFromAssemblies() and none of them registers
        // an inbox — they would each register a handler whose pipeline cannot be built.
        [UseInbox(step: 0, contextKey: nameof(GreetingEventHandler), onceOnly: true,
            onceOnlyAction: OnceOnlyAction.Warn)]
        public override GreetingEvent Handle(GreetingEvent greetingEvent)
        {
            Console.WriteLine("Received Greeting. Message Follows");
            Console.WriteLine("----------------------------------");
            Console.WriteLine(greetingEvent.Greeting);
            Console.WriteLine("----------------------------------");
            Console.WriteLine("Message Ends");
            return base.Handle(greetingEvent);
        }
    }
}
