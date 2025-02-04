using System;

namespace Paramore.Brighter;

/// <summary>
/// The <see cref="InMemoryMessageScheduler"/> factory
/// </summary>
/// <param name="timerProvider">The <see cref="TimeProvider"/>.</param>
public class InMemoryMessageSchedulerFactory(TimeProvider timerProvider) : IAmAMessageSchedulerFactory
{
    /// <summary>
    /// Get or create a scheduler id
    /// </summary>
    /// <remarks>
    /// The default approach is to use the message id. 
    /// </remarks>
    public Func<Message, string> GetOrCreateSchedulerId { get; set; } = message => message.Id;

    /// <summary>
    /// The action be executed on conflict during scheduler message
    /// </summary>
    public OnSchedulerConflict OnConflict { get; set; } = OnSchedulerConflict.Throw;
    
    public InMemoryMessageSchedulerFactory()
        : this(TimeProvider.System)
    {
    }

    public IAmAMessageScheduler Create(IAmACommandProcessor processor) 
        => new InMemoryMessageScheduler(processor, timerProvider, GetOrCreateSchedulerId, OnConflict);
}
 
