using System.Threading.Tasks;

namespace paramore.brighter.serviceactivator
{
    internal enum LampState
    {
        Dark=0,
        Lit=1
    }

    internal class Lamp
    {
        public IAmAPerformer Performer { get; private set; }
        public LampState State { get; set; }
        public Task Job { get; set; }

        public Lamp(IAmAMessageChannel channel, IAmAMessagePump messagePump)
        {
            Performer = new Performer(channel, messagePump);
            State = LampState.Dark;
        }

        public void Light()
        {
            State = LampState.Lit;
            Job = Performer.Run();
        }

        public void Snuff()
        {
            if (State == LampState.Lit)
            {
                Performer.Stop();
                State = LampState.Dark;
            }
        }
    }
}