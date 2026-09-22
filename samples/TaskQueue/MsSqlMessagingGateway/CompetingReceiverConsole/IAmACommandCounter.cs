namespace CompetingReceiverConsole
{
    public interface IAmACommandCounter
    {
        void CountCommand();
        int Counter { get; }
    }
}