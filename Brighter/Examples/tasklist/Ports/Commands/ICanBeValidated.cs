namespace Tasklist.Ports.Commands
{
    public interface ICanBeValidated
    {
        bool IsValid();
    }
}