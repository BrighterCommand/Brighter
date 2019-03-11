using System;
using Paramore.Brighter.Inbox.Attributes;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;

namespace Paramore.Brighter.Tests.OnceOnly.TestDoubles
{
    internal class MyStoredCommandToFailHandler : RequestHandler<MyCommandToFail>
    {
        [UseInbox(1, onceOnly: true, contextKey: typeof(MyStoredCommandToFailHandler), timing: HandlerTiming.Before)]
        public override MyCommandToFail Handle(MyCommandToFail command)
        {
            throw new NotImplementedException();
        }
    }
}
