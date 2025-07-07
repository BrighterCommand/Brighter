namespace Paramore.Brighter;

/// <summary>
/// The <see cref="IAmAMessageScheduler" /> factory.
/// </summary>
public interface IAmAMessageSchedulerFactory
{
    /// <summary>
    /// Get or create a new instance of <see cref="IAmAMessageScheduler"/>
    /// </summary>
    /// <param name="processor"></param>
    /// <returns>The <see cref="IAmAMessageScheduler"/>.</returns>
    IAmAMessageScheduler Create(IAmACommandProcessor processor);
}
