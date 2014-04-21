using System;

namespace paramore.brighter.serviceactivator
{
    public class Plug
    {
        public IAmAMessageChannel Cord { get; private set; }
        public Type Jack { get; private set; }
        public int NoOfPeformers { get; private set; }
        public int TimeoutInMiliseconds { get; private set; }

        public Plug(IAmAMessageChannel cord, Type jack, int noOfPeformers, int timeoutInMiliseconds)
        {
            Cord = cord;
            Jack = jack;
            NoOfPeformers = noOfPeformers;
            TimeoutInMiliseconds = timeoutInMiliseconds;
        }
    }
}