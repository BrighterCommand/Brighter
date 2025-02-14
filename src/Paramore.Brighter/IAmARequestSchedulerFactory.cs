namespace Paramore.Brighter;

/// <summary>
/// The <see cref="IAmARequestScheduler" /> factory.
/// </summary>
public interface IAmARequestSchedulerFactory
{
    /// <summary>
    /// Get or create a new instance of <see cref="IAmARequestSchedulerSync"/>
    /// </summary>
    /// <param name="processor"></param>
    /// <returns>The <see cref="IAmARequestSchedulerSync"/>.</returns>
    IAmARequestSchedulerSync CreateSync(IAmACommandProcessor processor);

    /// <summary>
    /// Get or create a new instance of <see cref="IAmARequestSchedulerAsync"/>
    /// </summary>
    /// <param name="processor"></param>
    /// <returns>The <see cref="IAmARequestSchedulerAsync"/>.</returns>
    IAmARequestSchedulerAsync CreateAsync(IAmACommandProcessor processor);
}
