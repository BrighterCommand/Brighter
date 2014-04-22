using System;

namespace paramore.brighter.serviceactivator
{
    public class Connection
    {
        public IAmAMessageChannel Channel { get; private set; }
        public Type DataType { get; private set; }
        public int NoOfPeformers { get; private set; }
        public int TimeoutInMiliseconds { get; private set; }

        public Connection(IAmAMessageChannel channel, Type dataType, int noOfPeformers, int timeoutInMiliseconds)
        {
            Channel = channel;
            DataType = dataType;
            NoOfPeformers = noOfPeformers;
            TimeoutInMiliseconds = timeoutInMiliseconds;
        }
    }
}