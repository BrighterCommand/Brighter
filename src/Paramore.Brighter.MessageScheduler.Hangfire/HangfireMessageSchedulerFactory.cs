using Hangfire;

namespace Paramore.Brighter.MessageScheduler.Hangfire;

/// <summary>
/// The <see cref="HangfireMessageScheduler"/> factory
/// </summary>
public class HangfireMessageSchedulerFactory(IBackgroundJobClientV2 client, string? queue) : IAmAMessageSchedulerFactory
{
    /// <inheritdoc cref="Create"/>
    public IAmAMessageScheduler Create(IAmACommandProcessor processor) 
        => new HangfireMessageScheduler(processor, client, queue);
}
