namespace Paramore.Brighter;

public interface IAmAMessageSchedulerFactory
{
    IAmAMessageScheduler Create(IAmACommandProcessor processor);
}
