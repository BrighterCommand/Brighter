using Events;

namespace CompetingReceiverConsole
{
    public class CommandCounter : IAmACommandCounter
    {
        public void CountCommand()
        {
            Counter++;
        }

        public int Counter { get; private set; }
    }
}
