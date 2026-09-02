#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Threading.Channels;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Attributes;

namespace Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles
{
    /// <summary>
    /// A workflow-step handler used by the end-to-end replay test. When it sees a command for the first time it records
    /// it in the inbox (via <see cref="OnceOnlyAction.Replay"/>) and forwards a downstream event onto the bus with
    /// <see cref="IAmACommandProcessor.Post{T}"/> — threading the pipeline's <see cref="RequestHandler{T}.Context"/> so
    /// the outgoing message inherits the inbound command's causation id. It then writes to a .NET
    /// <see cref="System.Threading.Channels.Channel{T}"/> so the test thread knows the message has been fully processed
    /// and can stop the pump. On a duplicate the pipeline short-circuits before this handler runs, so it does not fire
    /// again and does not forward again — the replay of the original outgoing message is driven by the outbox instead.
    /// </summary>
    internal sealed class ProcessAndForwardHandler : RequestHandler<MyCommand>
    {
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly ChannelWriter<MyCommand> _handled;

        public static int ReceivedCount { get; set; }
        public static Id? OutgoingMessageId { get; set; }

        public ProcessAndForwardHandler(IAmACommandProcessor commandProcessor, ChannelWriter<MyCommand> handled)
        {
            _commandProcessor = commandProcessor;
            _handled = handled;
        }

        [UseInbox(1, onceOnly: true, onceOnlyAction: OnceOnlyAction.Replay, contextKey: typeof(ProcessAndForwardHandler))]
        public override MyCommand Handle(MyCommand command)
        {
            ReceivedCount++;

            // Forward a downstream event. Post deposits it in the outbox and immediately dispatches it to the bus. We pass
            // the pipeline's request context so the outbox records it under the inbound command's causation id — the link
            // that lets a later duplicate replay exactly this message.
            var outgoing = new MyEvent();
            OutgoingMessageId = outgoing.Id;
            _commandProcessor.Post(outgoing, Context as RequestContext);

            // Signal the test thread that the handler has run to completion.
            _handled.TryWrite(command);

            return base.Handle(command);
        }
    }
}
