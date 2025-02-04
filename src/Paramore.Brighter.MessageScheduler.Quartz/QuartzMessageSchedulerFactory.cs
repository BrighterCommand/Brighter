using Quartz;

namespace Paramore.Brighter.MessageScheduler.Quartz;

/// <summary>
/// The <see cref="QuartzMessageScheduler"/> factory
/// </summary>
public class QuartzMessageSchedulerFactory(IScheduler scheduler) : IAmAMessageSchedulerFactory
{
    /// <summary>
    /// The Quartz Group.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Get or create scheduler
    /// </summary>
    public Func<Message, string> GetOrCreateSchedulerId { get; set; } = message => message.Id;
    
    /// <inheritdoc />
    public IAmAMessageScheduler Create(IAmACommandProcessor processor) 
        => new QuartzMessageScheduler(scheduler, Group, GetOrCreateSchedulerId);
}
