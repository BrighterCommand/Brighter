namespace Paramore.Brighter;

/// <summary>
/// The <see cref="IAmARequestScheduler" /> factory.
/// </summary>
public interface IAmARequestSchedulerFactory
{
    /// <summary>
    /// Get or create a new instance of <see cref="IAmARequestScheduler"/>
    /// </summary>
    /// <param name="processor"></param>
    /// <returns>The <see cref="IAmARequestScheduler"/>.</returns>
    IAmARequestScheduler Create(IAmACommandProcessor processor);
}
