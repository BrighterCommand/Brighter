using System;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox.Attributes;

namespace Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles
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
