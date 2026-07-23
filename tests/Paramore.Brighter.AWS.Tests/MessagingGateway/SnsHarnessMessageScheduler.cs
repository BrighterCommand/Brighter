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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway;

/// <summary>
/// A minimal message scheduler for the AWS SNS conformance harness (FR-9).
///
/// SNS has no native delayed publish: the gateway honours a requested delay by delegating to the
/// <see cref="IAmAMessageProducer.Scheduler"/> seam (<see cref="SnsMessageProducer.SendWithDelay"/>
/// hands the message to the scheduler when a non-zero delay is requested). The conformance suite
/// tests the *gateway's* delay-delegation contract, not Brighter's scheduler implementation (which
/// has its own dedicated tests), so the harness supplies a scheduler that honours the delay by
/// wall-clock and re-publishes the message to its SNS topic once the delay elapses — exactly the
/// universal-by-wall-clock behaviour FR-9 asserts. (FR-2 requeue-with-delay does not use this: it
/// is consumer-side <c>ChangeMessageVisibility</c> on the subscribed SQS queue.)
///
/// Timers and the producers used for redelivery are held and disposed with the scheduler.
/// </summary>
internal sealed class SnsHarnessMessageScheduler
    : IAmAMessageScheduler,
        IAmAMessageSchedulerSync,
        IAmAMessageSchedulerAsync,
        IDisposable
{
    private readonly AWSMessagingGatewayConnection _connection;
    private readonly List<Timer> _timers = [];
    private readonly List<SnsMessageProducer> _producers = [];
    private readonly object _lock = new();

    public SnsHarnessMessageScheduler(AWSMessagingGatewayConnection connection) =>
        _connection = connection;

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

    // ReScheduler / Cancel are not exercised by the FR-9 conformance behaviour.
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
            var publication = new SnsPublication
            {
                Topic = message.Header.Topic,
                MakeChannels = OnMissingChannel.Create,
            };

            var producer = new SnsMessageProducer(_connection, publication);
            lock (_lock)
            {
                _producers.Add(producer);
            }

            producer.Send(message);
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
                    producer.Dispose();
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
