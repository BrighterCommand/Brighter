using Hangfire;

namespace Paramore.Brighter.MessageScheduler.Hangfire;

/// <summary>
/// The <see cref="HangfireMessageScheduler"/> factory
/// </summary>
public class HangfireMessageSchedulerFactory : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
{
    /// <summary>
    /// The Hangfire queu
    /// </summary>
    public string? Queue { get; set; }

    private IBackgroundJobClientV2? _client;

    /// <summary>
    /// The <see cref="IBackgroundJobClientV2"/>. Lazily defaults to a <see cref="BackgroundJobClient"/>
    /// bound to <see cref="JobStorage.Current"/> only when first accessed without an explicit assignment.
    /// </summary>
    public IBackgroundJobClientV2 Client
    {
        get => _client ??= new BackgroundJobClient();
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
