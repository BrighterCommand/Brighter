using Hangfire;

namespace Paramore.Brighter.MessageScheduler.Hangfire;

/// <summary>
/// The <see cref="HangfireMessageScheduler"/> factory
/// </summary>
public class HangfireMessageSchedulerFactory : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
{
    private IBackgroundJobClientV2? _client;

    /// <summary>
    /// The Hangfire queu
    /// </summary>
    public string? Queue { get; set; }

    /// <summary>
    /// Gets or sets the client used to schedule Hangfire jobs.
    /// </summary>
    /// <remarks>
    /// The default client is created on first access using <see cref="JobStorage.Current"/>.
    /// Initialize Hangfire storage before creating a scheduler, or supply a client from the service provider.
    /// Constructing the factory does not require initialized storage.
    /// </remarks>
    public IBackgroundJobClientV2 Client
    {
        get => LazyInitializer.EnsureInitialized(ref _client, () => new BackgroundJobClient())!;
        set => _client = value;
    }

    /// <summary>
    /// The <see cref="System.TimeProvider"/>
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <inheritdoc />
    public IAmAMessageScheduler Create(IAmACommandProcessor processor)
        => new HangfireMessageScheduler(Client, Queue, TimeProvider);

    /// <inheritdoc />
    public IAmARequestSchedulerSync CreateSync(IAmACommandProcessor processor)
        => new HangfireMessageScheduler(Client, Queue, TimeProvider);

    /// <inheritdoc />
    public IAmARequestSchedulerAsync CreateAsync(IAmACommandProcessor processor)
        => new HangfireMessageScheduler(Client, Queue, TimeProvider);
}
