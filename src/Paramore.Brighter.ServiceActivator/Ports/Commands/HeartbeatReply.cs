using System.Collections.Generic;

namespace Paramore.Brighter.ServiceActivator.Ports.Commands
{
    public class HeartbeatReply : Reply
    {
        public HeartbeatReply(string hostName, ReplyAddress sendersAddress)
            :base(sendersAddress)
        {
            HostName = hostName;
            Consumers = new List<RunningConsumer>();
        }

        public string HostName { get; private set; }
        public IList<RunningConsumer> Consumers { get; private set; }
    }

    public class RunningConsumer
    {
        public RunningConsumer(ConsumerName consumerName, ConsumerState state)
        {
            ConsumerName = consumerName;
            State = state;
        }

        public ConsumerName ConsumerName { get; private set; }
        public ConsumerState State { get; private set; }
    }
}
