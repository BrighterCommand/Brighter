#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.Hosting;

/// <summary>
/// A hosted service that measures, on a fixed interval, how many messages in the outbox have not been dispatched and
/// are older than the stale age, and writes each result to the <see cref="OutboxStaleMessageReadings"/>.
/// </summary>
public partial class TimedOutboxStaleMessageMonitor : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly OutboxStaleMessageReadings _readings;
    private readonly TimedOutboxStaleMessageMonitorOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a monitor that is not yet running.
    /// </summary>
    /// <param name="serviceScopeFactory">The factory of the scope the monitor opens at start to resolve the outbox and the producers' configuration.</param>
    /// <param name="readings">The readings the monitor writes each measurement to.</param>
    /// <param name="options">The settings of the monitor.</param>
    /// <param name="timeProvider">The clock of the monitor; <see cref="TimeProvider.System"/> when null.</param>
    /// <exception cref="ConfigurationException">A setting in <paramref name="options"/> is outside the range the monitor accepts.</exception>
    public TimedOutboxStaleMessageMonitor(
        IServiceScopeFactory serviceScopeFactory,
        OutboxStaleMessageReadings readings,
        TimedOutboxStaleMessageMonitorOptions options,
        TimeProvider? timeProvider = null)
    {
        options.Validate();

        _serviceScopeFactory = serviceScopeFactory;
        _readings = readings;
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Starts the monitor.
    /// </summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ConfigurationException">The container has no outbox or no producers' configuration, because <c>AddProducers</c> was not called; or the
    /// <see cref="Publication.StaleMessageAge"/> of a publication is not greater than zero and at most <see cref="TimedOutboxStaleMessageMonitorOptions.MaxStaleMessageAge"/>.</exception>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetService<IAmAnOutbox>();
        var producersConfiguration = scope.ServiceProvider.GetService<IAmProducersConfiguration>();
        var transactionProvider = scope.ServiceProvider.GetService<IAmABoxTransactionProvider>();

        if (outbox is null || producersConfiguration is null)
            throw new ConfigurationException(
                $"The outbox stale-message monitor needs an {nameof(IAmAnOutbox)} and an {nameof(IAmProducersConfiguration)}; register them with AddProducers");

        var publications = ReadPublications(producersConfiguration);
        ValidateStaleMessageAges(publications);

        return Task.CompletedTask;
    }

    private static IReadOnlyList<Publication> ReadPublications(IAmProducersConfiguration producersConfiguration) =>
        producersConfiguration.ProducerRegistry?.Producers.Select(producer => producer.Publication).ToList()
        ?? new List<Publication>();

    private static void ValidateStaleMessageAges(IEnumerable<Publication> publications)
    {
        var publication = publications.FirstOrDefault(HasInvalidStaleMessageAge);
        if (publication is not null)
            throw new ConfigurationException(
                $"The {nameof(Publication.StaleMessageAge)} of the publication for topic {publication.Topic} must be greater than zero and at most {TimedOutboxStaleMessageMonitorOptions.MaxStaleMessageAge}, but was {publication.StaleMessageAge}");
    }

    private static bool HasInvalidStaleMessageAge(Publication publication) =>
        publication.StaleMessageAge is { } staleMessageAge
        && (staleMessageAge <= TimeSpan.Zero || staleMessageAge > TimedOutboxStaleMessageMonitorOptions.MaxStaleMessageAge);

    /// <summary>
    /// Stops the monitor.
    /// </summary>
    /// <param name="cancellationToken">The host's stop token.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Signals the monitor to stop and returns without waiting.
    /// </summary>
    public void Dispose()
    {
    }
}
