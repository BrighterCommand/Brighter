using System.Threading.Tasks;

namespace paramore.brighter.serviceactivator
{
    internal enum ConsumerState
    {
        Sleeping=0,
        Awake=1
    }

    internal class Consumer
    {
        public IAmAPerformer Performer { get; private set; }
        public ConsumerState State { get; set; }
        public Task Job { get; set; }

        public Consumer(IAmAMessageChannel channel, IAmAMessagePump messagePump)
        {
            Performer = new Performer(channel, messagePump);
            State = ConsumerState.Sleeping;
        }

        public void Wake()
        {
            State = ConsumerState.Awake;
            Job = Performer.Run();
        }

        public void Sleep()
        {
            if (State == ConsumerState.Awake)
            {
                Performer.Stop();
                State = ConsumerState.Sleeping;
            }
        }
    }
}