#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.MQTT;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway;

/// <summary>
/// A minimal message scheduler for the MQTT conformance harness (FR-2, FR-9).
///
/// MQTT has no native delayed delivery: the gateway honours a requested delay by delegating to
/// the scheduler seam. <see cref="MqttMessageProducer.SendWithDelay"/> (FR-9) and
/// <see cref="MqttMessageConsumer.Requeue"/> (FR-2, via a requeue producer's <c>SendWithDelay</c>)
/// both throw a <see cref="ConfigurationException"/> when no scheduler is configured. The conformance
/// suite tests the <em>gateway's</em> delay-delegation contract, not Brighter's scheduler
/// implementation (which has its own dedicated tests), so the harness supplies a scheduler that
/// honours the delay by wall-clock and re-publishes the message to its topic once the delay elapses —
/// exactly the universal-by-wall-clock behaviour FR-2 / FR-9 assert.
///
/// The same instance is shared by a provider's producer path (delayed send, FR-9) and its consumer
/// path (delayed requeue, FR-2, which the gateway routes through a requeue producer's
/// <c>SendWithDelay</c>). Timers and the producers used for redelivery are held and disposed with
/// the scheduler.
/// </summary>
internal sealed class MqttHarnessMessageScheduler
    : IAmAMessageScheduler,
        IAmAMessageSchedulerSync,
        IAmAMessageSchedulerAsync,
        IDisposable
{
    private readonly string _hostname;
    private readonly int _port;
    private readonly List<Timer> _timers = [];
    private readonly List<MqttMessageProducer> _producers = [];
    private readonly object _lock = new();

    public MqttHarnessMessageScheduler(string hostname = "localhost", int port = 1883)
    {
        _hostname = hostname;
        _port = port;
    }

    public string Schedule(Message message, TimeSpan delay)
    {
        var id = Guid.NewGuid().ToString();
        var timer = new Timer(_ => Republish(message), null, Clamp(delay), Timeout.InfiniteTimeSpan);
        lock (_lock) { _timers.Add(timer); }
        return id;
    }

    public string Schedule(Message message, DateTimeOffset at) =>
        Schedule(message, at - DateTimeOffset.UtcNow);

    public Task<string> ScheduleAsync(
        Message message,
        TimeSpan delay,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Schedule(message, delay));

    public Task<string> ScheduleAsync(
        Message message,
        DateTimeOffset at,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Schedule(message, at));

    // ReSchedule / Cancel are not exercised by the FR-2 / FR-9 conformance behaviours.
    public bool ReScheduler(string schedulerId, DateTimeOffset at) => false;

    public bool ReScheduler(string schedulerId, TimeSpan delay) => false;

    public Task<bool> ReSchedulerAsync(
        string schedulerId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> ReSchedulerAsync(
        string schedulerId,
        TimeSpan delay,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public void Cancel(string id) { }

    public Task CancelAsync(string id, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private static TimeSpan Clamp(TimeSpan delay) => delay > TimeSpan.Zero ? delay : TimeSpan.Zero;

    /// <summary>
    /// Republishes <paramref name="message"/> to its topic after the timer fires.
    /// A new producer is created per republish (MQTT client connections are cheap on localhost).
    /// </summary>
    private void Republish(Message message)
    {
        try
        {
            // The message's topic is the routing key used by both the producer prefix and
            // the subscriber's wildcard pattern. Publishing to {topicPrefix}/{Header.Topic}
            // (where topicPrefix == Header.Topic) mirrors the pattern the provider's main producer
            // follows, so the subscriber's "{routingKey}/#" wildcard catches the redelivered message.
            var topicPrefix = message.Header.Topic.Value;
            var config = new MqttMessagingGatewayProducerConfiguration
            {
                Hostname = _hostname,
                Port = _port,
                TopicPrefix = topicPrefix,
                ClientID = $"brighter-sched-{Guid.NewGuid().ToString("N")[..8]}"
            };
            var publisher = new MqttMessagePublisher(config);
            var producer = new MqttMessageProducer(publisher, new Publication { Topic = message.Header.Topic });
            lock (_lock) { _producers.Add(producer); }
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
                try { timer.Dispose(); }
                catch { /* Ignore disposal errors */ }
            }

            _timers.Clear();

            foreach (var producer in _producers)
            {
                try { producer.Dispose(); }
                catch { /* Ignore disposal errors */ }
            }

            _producers.Clear();
        }
    }
}
