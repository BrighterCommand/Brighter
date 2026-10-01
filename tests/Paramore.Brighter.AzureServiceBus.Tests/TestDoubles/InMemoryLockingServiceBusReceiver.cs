#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;

public class InMemoryLockingServiceBusReceiver(int messageCount, TimeSpan lockDuration) : ServiceBusReceiver
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _locks = new();
    private readonly HashSet<string> _completed = new();
    private int _remaining = messageCount;
    private int _renewals;
    private int _renewalAttempts;
    private int _renewalsInFlight;
    private int _abandoned;
    private int _deadLettered;

    public string? ContentType { get; set; }
    public Exception? ReceiveException { get; set; }
    public bool BlockRenewal { get; set; }
    public bool HoldCancelledRenewal { get; set; }
    public TaskCompletionSource<bool> RenewalCancellationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> AllowRenewalToFinish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int RenewalFailuresRemaining { get; set; }
    public bool RenewalFailureIsTransient { get; set; }
    public TaskCompletionSource<bool> RenewalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> RenewalSucceeded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int RenewalAttemptCount => Volatile.Read(ref _renewalAttempts);
    public int RenewalsInFlight => Volatile.Read(ref _renewalsInFlight);
    public int AbandonedCount => Volatile.Read(ref _abandoned);
    public int DeadLetteredCount => Volatile.Read(ref _deadLettered);
    private bool _closed;

    public int CompletedCount
    {
        get { lock (_gate) return _completed.Count; }
    }

    public int RenewalCount
    {
        get { lock (_gate) return _renewals; }
    }

    public override bool IsClosed
    {
        get { lock (_gate) return _closed; }
    }

    public override Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(
        int maxMessages, TimeSpan? maxWaitTime = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ReceiveException is not null) throw ReceiveException;
        lock (_gate)
        {
            if (_closed) throw new ObjectDisposedException(nameof(InMemoryLockingServiceBusReceiver));

            var messages = new List<ServiceBusReceivedMessage>();
            var lockedUntil = DateTimeOffset.UtcNow + lockDuration;
            while (_remaining > 0 && messages.Count < maxMessages)
            {
                var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                    body: new BinaryData("{}"),
                    messageId: Guid.NewGuid().ToString(),
                    contentType: ContentType,
                    properties: new Dictionary<string, object> { ["MessageType"] = "MT_COMMAND" },
                    lockTokenGuid: Guid.NewGuid(),
                    deliveryCount: 1,
                    lockedUntil: lockedUntil);
                _locks.Add(message.LockToken, lockedUntil);
                messages.Add(message);
                _remaining--;
            }

            return Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>(messages);
        }
    }

    public override async Task RenewMessageLockAsync(ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _renewalAttempts);
        Interlocked.Increment(ref _renewalsInFlight);
        RenewalStarted.TrySetResult(true);
        try
        {
            if (BlockRenewal) await Task.Delay(Timeout.Infinite, cancellationToken);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                EnsureLockIsHeld(message.LockToken);
                if (RenewalFailuresRemaining > 0)
                {
                    RenewalFailuresRemaining--;
                    throw new ServiceBusException("Renewal failed.",
                        RenewalFailureIsTransient ? ServiceBusFailureReason.ServiceBusy : ServiceBusFailureReason.MessageLockLost);
                }

                var lockedUntil = DateTimeOffset.UtcNow + lockDuration;
                _locks[message.LockToken] = lockedUntil;
                message.GetRawAmqpMessage().MessageAnnotations["x-opt-locked-until"] = lockedUntil.UtcDateTime;
                _renewals++;
                RenewalSucceeded.TrySetResult(true);
            }
        }
        catch (OperationCanceledException) when (HoldCancelledRenewal)
        {
            RenewalCancellationStarted.TrySetResult(true);
            await AllowRenewalToFinish.Task;
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _renewalsInFlight);
        }
    }

    public void RenewSessionLocks(DateTimeOffset lockedUntil)
    {
        lock (_gate)
        {
            foreach (var token in new List<string>(_locks.Keys))
                _locks[token] = lockedUntil;
        }
    }

    public override Task CompleteMessageAsync(ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            EnsureLockIsHeld(message.LockToken);
            _locks.Remove(message.LockToken);
            _completed.Add(message.LockToken);
            return Task.CompletedTask;
        }
    }

    public override Task AbandonMessageAsync(ServiceBusReceivedMessage message,
        IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            EnsureLockIsHeld(message.LockToken);
            _locks.Remove(message.LockToken);
            _abandoned++;
            return Task.CompletedTask;
        }
    }

    public override Task DeadLetterMessageAsync(ServiceBusReceivedMessage message,
        string deadLetterReason, string? deadLetterErrorDescription = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            EnsureLockIsHeld(message.LockToken);
            _locks.Remove(message.LockToken);
            _deadLettered++;
            return Task.CompletedTask;
        }
    }

    public override Task CloseAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _closed = true;
            _locks.Clear();
            return Task.CompletedTask;
        }
    }

    private void EnsureLockIsHeld(string lockToken)
    {
        if (_closed || !_locks.TryGetValue(lockToken, out var lockedUntil) || lockedUntil <= DateTimeOffset.UtcNow)
            throw new ServiceBusException("The message lock has expired or been released.",
                ServiceBusFailureReason.MessageLockLost);
    }
}
