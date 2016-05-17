using System;
using Brighter.Example.Messages;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;

namespace ProtoBufReceiver.MessageHandlers
{
    public class SampleCommandHandler : RequestHandler<SampleCommand>
    {
        public SampleCommandHandler(ILog logger)
            : base(logger)
        {
           
        }

        public override SampleCommand Handle(SampleCommand command)
        {
            Console.WriteLine("Received Command");
            Console.WriteLine(command.ToString());
            return base.Handle(command);
        }
    }
}
