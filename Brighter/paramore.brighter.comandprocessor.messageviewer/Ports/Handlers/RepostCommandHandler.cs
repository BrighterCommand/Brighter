using System;
using System.Collections.Generic;

namespace paramore.brighter.commandprocessor.messageviewer.Ports.Handlers
{
    public class RepostCommand : ICommand
    {
        public List<string> MessageIds { get; set; }
    }

    public class RepostCommandHandler : IHandleCommand<RepostCommand>
    {
        public void Handle(RepostCommand command)
        {
            throw new NotImplementedException();
        }
    }
}