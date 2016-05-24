using System;
using Brighter.Example.Messages;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;

namespace ProtoBufReceiver.MessageHandlers
{
    public class SampleEventHandler : RequestHandler<SampleEvent>
    {
        public SampleEventHandler(ILog logger)
            : base(logger)
        {

        }

        public override SampleEvent Handle(SampleEvent message)
        {
            Console.WriteLine("Received Event");
            Console.WriteLine(message.ToString());
            return base.Handle(message);
        }
    }
}
