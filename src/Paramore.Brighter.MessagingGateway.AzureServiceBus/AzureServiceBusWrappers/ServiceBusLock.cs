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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

internal sealed partial class ServiceBusLock
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<ServiceBusLock>();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _renewal;
    private readonly string _entityPath;
    private readonly string _lockType;
    private readonly string _lockId;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private long _lockedUntilTicks;
    private int _lost;
    private Task? _stopping;

    public ServiceBusLock(DateTimeOffset lockedUntil, TimeSpan maxDuration,
        string entityPath, string lockType, string lockId, Func<CancellationToken, Task<DateTimeOffset>> renew)
    {
        _lockedUntilTicks = lockedUntil.UtcTicks;
        _entityPath = entityPath;
        _lockType = lockType;
        _lockId = lockId;
        _renewal = maxDuration == TimeSpan.Zero || lockedUntil == default
            ? Task.CompletedTask
            : Task.Run(() => RenewAsync(maxDuration, renew));
    }

    public bool IsValid
    {
        get
        {
            var lockedUntilTicks = Interlocked.Read(ref _lockedUntilTicks);
            return Volatile.Read(ref _lost) == 0 &&
                (lockedUntilTicks == 0 || lockedUntilTicks > DateTimeOffset.UtcNow.UtcTicks);
        }
    }

    public Task StopAsync()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopping = Interlocked.CompareExchange(ref _stopping, completion.Task, null);
        if (stopping is not null) return stopping;

        _ = StopCoreAsync(completion);
        return completion.Task;
    }

    private async Task StopCoreAsync(TaskCompletionSource<bool> completion)
    {
        try
        {
            try
            {
                _stop.Cancel();
            }
            finally
            {
                await _renewal.ConfigureAwait(false);
                _stop.Dispose();
            }
            completion.TrySetResult(true);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private async Task RenewAsync(TimeSpan maxDuration, Func<CancellationToken, Task<DateTimeOffset>> renew)
    {
        var retryDelayMilliseconds = 100d;
        var retrying = false;
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var budget = maxDuration - _elapsed.Elapsed;
                if (budget <= TimeSpan.Zero)
                {
                    LogRenewalBudgetReached(maxDuration);
                    return;
                }

                var remainingLock = new DateTimeOffset(Interlocked.Read(ref _lockedUntilTicks), TimeSpan.Zero) - DateTimeOffset.UtcNow;
                var buffer = Math.Min(remainingLock.TotalMilliseconds / 2, 10000);
                var delay = TimeSpan.FromMilliseconds(Math.Max(1, remainingLock.TotalMilliseconds - buffer));
                if (delay >= budget)
                {
                    await Task.Delay(budget, _stop.Token).ConfigureAwait(false);
                    LogRenewalBudgetReached(maxDuration);
                    return;
                }
                await Task.Delay(delay, _stop.Token).ConfigureAwait(false);

                budget = maxDuration - _elapsed.Elapsed;
                if (budget <= TimeSpan.Zero)
                {
                    LogRenewalBudgetReached(maxDuration);
                    return;
                }
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                attempt.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(budget.TotalMilliseconds, int.MaxValue)));
                try
                {
                    var lockedUntil = await renew(attempt.Token).ConfigureAwait(false);
                    Interlocked.Exchange(ref _lockedUntilTicks, lockedUntil.UtcTicks);
                    retryDelayMilliseconds = 100;
                    retrying = false;
                }
                catch (OperationCanceledException) when (attempt.IsCancellationRequested)
                {
                    LogRenewalBudgetReached(maxDuration);
                    return;
                }
                catch (ServiceBusException exception) when (exception.IsTransient)
                {
                    var lockedUntil = new DateTimeOffset(Interlocked.Read(ref _lockedUntilTicks), TimeSpan.Zero);
                    if (retrying)
                        Log.TransientRenewalRetry(s_logger, exception, _lockType, _lockId, _entityPath, maxDuration, lockedUntil);
                    else
                        Log.TransientRenewalFailure(s_logger, exception, _lockType, _lockId, _entityPath, maxDuration, lockedUntil);
                    retrying = true;

                    budget = maxDuration - _elapsed.Elapsed;
                    if (budget <= TimeSpan.Zero)
                    {
                        LogRenewalBudgetReached(maxDuration);
                        return;
                    }
                    remainingLock = lockedUntil - DateTimeOffset.UtcNow;
                    var retryDelay = remainingLock > TimeSpan.Zero
                        ? Math.Min(retryDelayMilliseconds, remainingLock.TotalMilliseconds)
                        : retryDelayMilliseconds;
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(retryDelay, budget.TotalMilliseconds)), _stop.Token).ConfigureAwait(false);
                    retryDelayMilliseconds = Math.Min(retryDelayMilliseconds * 2, 1000);
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (exception is ServiceBusException { Reason: ServiceBusFailureReason.MessageLockLost or ServiceBusFailureReason.SessionLockLost })
                Interlocked.Exchange(ref _lost, 1);
            Log.RenewalStoppedAfterFailure(s_logger, exception, _lockType, _lockId, _entityPath, maxDuration,
                new DateTimeOffset(Interlocked.Read(ref _lockedUntilTicks), TimeSpan.Zero));
        }
    }

    private void LogRenewalBudgetReached(TimeSpan maxDuration)
    {
        if (_stop.IsCancellationRequested) return;

        Log.RenewalBudgetReached(s_logger, _lockType, _lockId, _entityPath, maxDuration,
            new DateTimeOffset(Interlocked.Read(ref _lockedUntilTicks), TimeSpan.Zero));
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Transient failure renewing Service Bus {LockType} lock for {LockId} on {EntityPath}. Configured renewal duration is {MaxAutoLockRenewalDuration}; last known lock expiry is {LockedUntil}")]
        public static partial void TransientRenewalFailure(ILogger logger, Exception exception, string lockType,
            string lockId, string entityPath, TimeSpan maxAutoLockRenewalDuration, DateTimeOffset lockedUntil);

        [LoggerMessage(LogLevel.Debug, "Transient failure retrying Service Bus {LockType} lock renewal for {LockId} on {EntityPath}. Configured renewal duration is {MaxAutoLockRenewalDuration}; last known lock expiry is {LockedUntil}")]
        public static partial void TransientRenewalRetry(ILogger logger, Exception exception, string lockType,
            string lockId, string entityPath, TimeSpan maxAutoLockRenewalDuration, DateTimeOffset lockedUntil);

        [LoggerMessage(LogLevel.Warning, "Service Bus {LockType} lock renewal for {LockId} on {EntityPath} stopped after a failure. Configured renewal duration is {MaxAutoLockRenewalDuration}; last known lock expiry is {LockedUntil}")]
        public static partial void RenewalStoppedAfterFailure(ILogger logger, Exception exception, string lockType,
            string lockId, string entityPath, TimeSpan maxAutoLockRenewalDuration, DateTimeOffset lockedUntil);

        [LoggerMessage(LogLevel.Warning, "Stopped automatic Service Bus {LockType} lock renewal for {LockId} on {EntityPath}: MaxAutoLockRenewalDuration {MaxAutoLockRenewalDuration} has been reached. Last known lock expiry is {LockedUntil}; processing is not cancelled")]
        public static partial void RenewalBudgetReached(ILogger logger, string lockType, string lockId,
            string entityPath, TimeSpan maxAutoLockRenewalDuration, DateTimeOffset lockedUntil);
    }
}
