using System;

namespace Paramore.Brighter;

/// <summary>
/// The <see cref="InMemoryScheduler"/> factory
/// </summary>
public class InMemorySchedulerFactory : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
{
    /// <summary>
    /// The <see cref="System.TimeProvider"/>.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    
    /// <summary>
    /// Get or create a scheduler id for a message
    /// </summary>
    /// <remarks>
    /// The default approach is generate a Guid 
    /// </remarks>
    public Func<Message, string> GetOrCreateMessageSchedulerId { get; set; } = _ => Id.Random().Value;

    /// <summary>
    /// Get or create  a scheduler id to a request
    /// </summary>
    /// <remarks>
    /// The default approach is generate a Guid 
    /// </remarks>
    public Func<IRequest, string> GetOrCreateRequestSchedulerId { get; set; } = _ => Id.Random().Value;

    /// <summary>
    /// The action be executed on conflict during scheduler message
    /// </summary>
    public OnSchedulerConflict OnConflict { get; set; } = OnSchedulerConflict.Throw;
    
    /// <inheritdoc />
    public IAmAMessageScheduler Create(IAmACommandProcessor processor) 
        => new InMemoryScheduler(processor, TimeProvider, GetOrCreateRequestSchedulerId, GetOrCreateMessageSchedulerId, OnConflict);

    /// <inheritdoc />
    public IAmARequestSchedulerSync CreateSync(IAmACommandProcessor processor)
        => new InMemoryScheduler(processor, TimeProvider, GetOrCreateRequestSchedulerId, GetOrCreateMessageSchedulerId, OnConflict);

    /// <inheritdoc />
    public IAmARequestSchedulerAsync CreateAsync(IAmACommandProcessor processor)
        => new InMemoryScheduler(processor, TimeProvider, GetOrCreateRequestSchedulerId, GetOrCreateMessageSchedulerId, OnConflict);
}
 
