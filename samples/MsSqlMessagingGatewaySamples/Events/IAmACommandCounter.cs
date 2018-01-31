namespace Events
{
    public interface IAmACommandCounter
    {
        void CountCommand();
        int Counter { get; }
    }
}