using Quartz;

namespace Paramore.Brighter.MessageScheduler.Quartz;

/// <summary>
/// The <see cref="QuartzScheduler"/> factory
/// </summary>
public class QuartzSchedulerFactory(IScheduler scheduler) : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
{
    /// <summary>
    /// The <see cref="System.TimeProvider"/>
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    
    /// <summary>
    /// The Quartz Group.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Get or create scheduler
    /// </summary>
    /// <remarks>
    /// The default approach is generate a Guid 
    /// </remarks>
    public Func<Message, string> GetOrCreateSchedulerId { get; set; } = _ => Uuid.NewAsString();
    
    /// <summary>
    /// Get or create scheduler
    /// </summary>
    /// <remarks>
    /// The default approach is generate a Guid 
    /// </remarks>
    public Func<IRequest, string> GetOrCreateRequestSchedulerId { get; set; } = _ => Uuid.NewAsString();
    
    /// <inheritdoc />
    public IAmAMessageScheduler Create(IAmACommandProcessor processor) 
        => new QuartzScheduler(scheduler, Group, TimeProvider, GetOrCreateSchedulerId, GetOrCreateRequestSchedulerId);

    /// <inheritdoc />
    public IAmARequestSchedulerSync CreateSync(IAmACommandProcessor processor)
        => new QuartzScheduler(scheduler, Group, TimeProvider, GetOrCreateSchedulerId, GetOrCreateRequestSchedulerId);

    /// <inheritdoc />
    public IAmARequestSchedulerAsync CreateAsync(IAmACommandProcessor processor)
        => new QuartzScheduler(scheduler, Group, TimeProvider, GetOrCreateSchedulerId, GetOrCreateRequestSchedulerId);
}
