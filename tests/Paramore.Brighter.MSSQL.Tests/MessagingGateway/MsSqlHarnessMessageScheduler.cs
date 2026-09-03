#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

/// <summary>
/// A minimal message scheduler for the MSSQL conformance harness (FR-2, FR-9).
///
/// MSSQL has no native delayed delivery: the gateway honours a requested delay by delegating to
/// the scheduler seam. <see cref="MsSqlMessageProducer.SendWithDelay"/> (FR-9) and
/// <see cref="MsSqlMessageConsumer.Requeue"/> (FR-2, via a requeue producer's <c>SendWithDelay</c>)
/// both throw a <see cref="ConfigurationException"/> when no scheduler is configured. The conformance
/// suite tests the *gateway's* delay-delegation contract, not Brighter's scheduler implementation
/// (which has its own dedicated tests), so the harness supplies a scheduler that honours the delay
/// by wall-clock and re-publishes the message to its topic once the delay elapses — exactly the
/// universal-by-wall-clock behaviour FR-2 / FR-9 assert.
///
/// The same instance is shared by a provider's producer path (delayed send, FR-9) and its consumer
/// path (delayed requeue, FR-2, which the gateway routes through a requeue producer's
/// <c>SendWithDelay</c>). Timers and the producers used for redelivery are held and disposed with
/// the scheduler.
/// </summary>
internal sealed class MsSqlHarnessMessageScheduler
    : IAmAMessageScheduler,
        IAmAMessageSchedulerSync,
        IAmAMessageSchedulerAsync,
        IDisposable
{
    private readonly RelationalDatabaseConfiguration _configuration;
    private readonly List<Timer> _timers = [];
    private readonly List<IAmAMessageProducer> _producers = [];
    private readonly object _lock = new();

    public MsSqlHarnessMessageScheduler(RelationalDatabaseConfiguration configuration) =>
        _configuration = configuration;

    public string Schedule(Message message, TimeSpan delay)
    {
        var id = Guid.NewGuid().ToString();
        var timer = new Timer(_ => Republish(message), null, Clamp(delay), Timeout.InfiniteTimeSpan);
        lock (_lock)
        {
            _timers.Add(timer);
        }

        return id;
    }

    public string Schedule(Message message, DateTimeOffset at) =>
        Schedule(message, at - DateTimeOffset.UtcNow);

    public Task<string> ScheduleAsync(
        Message message,
        TimeSpan delay,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(Schedule(message, delay));

    public Task<string> ScheduleAsync(
        Message message,
        DateTimeOffset at,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(Schedule(message, at));

    // ReScheduler / Cancel are not exercised by the FR-2 / FR-9 conformance behaviours.
    public bool ReScheduler(string schedulerId, DateTimeOffset at) => false;

    public bool ReScheduler(string schedulerId, TimeSpan delay) => false;

    public Task<bool> ReSchedulerAsync(
        string schedulerId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(false);

    public Task<bool> ReSchedulerAsync(
        string schedulerId,
        TimeSpan delay,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(false);

    public void Cancel(string id) { }

    public Task CancelAsync(string id, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private static TimeSpan Clamp(TimeSpan delay) => delay > TimeSpan.Zero ? delay : TimeSpan.Zero;

    private void Republish(Message message)
    {
        try
        {
            var publication = new Publication
            {
                Topic = message.Header.Topic,
                MakeChannels = OnMissingChannel.Create,
            };

            var producer = new MsSqlMessageProducerFactory(_configuration, [publication])
                .Create()
                .First()
                .Value;
            lock (_lock)
            {
                _producers.Add(producer);
            }

            ((IAmAMessageProducerSync)producer).Send(message);
        }
        catch
        {
            // Best-effort redelivery for the conformance harness; a broker error surfaces as the
            // conformance test's after-delay arm timing out rather than an unobserved exception.
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var timer in _timers)
            {
                try
                {
                    timer.Dispose();
                }
                catch
                {
                    // Ignore any error during disposing
                }
            }

            _timers.Clear();

            foreach (var producer in _producers)
            {
                try
                {
                    (producer as IDisposable)?.Dispose();
                }
                catch
                {
                    // Ignore any error during disposing
                }
            }

            _producers.Clear();
        }
    }
}
