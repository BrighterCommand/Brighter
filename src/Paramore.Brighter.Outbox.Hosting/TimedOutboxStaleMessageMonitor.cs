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
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Paramore.Brighter.Outbox.Hosting;

/// <summary>
/// A hosted service that measures, on a fixed interval, how many messages in the outbox have not been dispatched and
/// are older than the stale age, and writes each result to the <see cref="OutboxStaleMessageReadings"/>.
/// </summary>
public partial class TimedOutboxStaleMessageMonitor : IHostedService, IDisposable
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<TimedOutboxStaleMessageMonitor>();

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
    /// <remarks>
    /// Starts the <see cref="OutboxStaleMessageReadings"/> with the outbox's type name, and with topics when the outbox is an <see cref="IAmATopicCountingOutbox"/>
    /// and at least one publication has a topic that is not empty.
    /// Otherwise logs one Information entry saying that only the overall count is measured, and why. When the outbox cannot count by topic, that is the reason given.
    /// </remarks>
    /// <returns>A completed task.</returns>
    /// <exception cref="ConfigurationException">The container has no <see cref="IAmAnOutbox"/> or no <see cref="IAmProducersConfiguration"/>, which <c>AddProducers</c> registers; or the outbox is sync-only, so it does not implement
    /// <see cref="IAmAnOutboxAsync{T, TTransaction}"/> for the transaction type of the <see cref="IAmABoxTransactionProvider"/>; or the
    /// <see cref="Publication.StaleMessageAge"/> of a publication is zero or less, or greater than <see cref="TimedOutboxStaleMessageMonitorOptions.MaxStaleMessageAge"/>.</exception>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetService<IAmAnOutbox>();
        var producersConfiguration = scope.ServiceProvider.GetService<IAmProducersConfiguration>();
        var transactionProvider = scope.ServiceProvider.GetService<IAmABoxTransactionProvider>();

        if (outbox is null)
            throw new ConfigurationException($"The outbox stale-message monitor found no {nameof(IAmAnOutbox)} in the container; AddProducers registers it");
        if (producersConfiguration is null)
            throw new ConfigurationException($"The outbox stale-message monitor found no {nameof(IAmProducersConfiguration)} in the container; AddProducers registers it");
        ValidateOutboxCountsAsynchronously(outbox, transactionProvider);

        var publications = ReadPublications(producersConfiguration);
        ValidateStaleMessageAges(publications);
        var topicAges = BuildTopicAges(publications);

        var outboxType = outbox.GetType().Name;
        var countsByTopic = outbox is IAmATopicCountingOutbox;
        LogWhenTopicsAreNotMeasured(outboxType, countsByTopic, topicAges.Count);

        _readings.Start(outboxType, countsByTopic && topicAges.Count > 0);
        return Task.CompletedTask;
    }

    private IReadOnlyDictionary<RoutingKey, TimeSpan> BuildTopicAges(IEnumerable<Publication> publications) =>
        new ReadOnlyDictionary<RoutingKey, TimeSpan>(publications
            .Where(publication => !RoutingKey.IsNullOrEmpty(publication.Topic))
            .GroupBy(publication => publication.Topic!)
            .ToDictionary(
                topic => topic.Key,
                topic => topic.Min(publication => publication.StaleMessageAge ?? _options.StaleMessageAge)));

    private static void LogWhenTopicsAreNotMeasured(string outboxType, bool countsByTopic, int topicCount)
    {
        if (!countsByTopic)
            Log.OverallCountOnlyWithNoCapability(s_logger, outboxType);
        else if (topicCount == 0)
            Log.OverallCountOnlyWithNoMeasuredTopic(s_logger, outboxType);
    }

    private static void ValidateOutboxCountsAsynchronously(IAmAnOutbox outbox, IAmABoxTransactionProvider? transactionProvider)
    {
        var transactionType = CommandProcessor.GetTransactionTypeFromTransactionProvider(transactionProvider);
        var asyncOutboxType = typeof(IAmAnOutboxAsync<,>).MakeGenericType(typeof(Message), transactionType);
        if (!asyncOutboxType.IsInstanceOfType(outbox))
            throw new ConfigurationException(
                $"The outbox stale-message monitor needs an outbox that counts asynchronously, but {outbox.GetType().Name} is sync-only");
    }

    private static IReadOnlyList<Publication> ReadPublications(IAmProducersConfiguration producersConfiguration) =>
        producersConfiguration.ProducerRegistry?.Producers.Select(producer => producer.Publication).ToList()
        ?? new List<Publication>();

    private static void ValidateStaleMessageAges(IEnumerable<Publication> publications)
    {
        var publication = publications.FirstOrDefault(HasInvalidStaleMessageAge);
        if (publication is not null)
            throw new ConfigurationException(
                $"The {nameof(Publication.StaleMessageAge)} of {DescribePublication(publication)} must be greater than zero and at most {TimedOutboxStaleMessageMonitorOptions.MaxStaleMessageAge}, but was {publication.StaleMessageAge}");
    }

    private static string DescribePublication(Publication publication) =>
        RoutingKey.IsNullOrEmpty(publication.Topic)
            ? "a publication with an empty topic"
            : $"the publication for topic {publication.Topic}";

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

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information, "Outbox stale-message monitor measures only the overall count for {OutboxType}: no capability to count by topic")]
        public static partial void OverallCountOnlyWithNoCapability(ILogger logger, string outboxType);

        [LoggerMessage(LogLevel.Information, "Outbox stale-message monitor measures only the overall count for {OutboxType}: no measured topic")]
        public static partial void OverallCountOnlyWithNoMeasuredTopic(ILogger logger, string outboxType);
    }
}
