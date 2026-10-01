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

internal sealed class ServiceBusLock
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<ServiceBusLock>();
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private readonly Task _renewal;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private long _lockedUntilTicks;
    private int _lost;
    private Task? _stopping;

    public ServiceBusLock(DateTimeOffset lockedUntil, TimeSpan maxDuration,
        Func<CancellationToken, Task<DateTimeOffset>> renew)
    {
        _lockedUntilTicks = lockedUntil.UtcTicks;
        _renewal = maxDuration == TimeSpan.Zero || lockedUntil == default
            ? Task.CompletedTask
            : Task.Run(() => RenewAsync(maxDuration, renew));
    }

    public bool IsValid => Volatile.Read(ref _lost) == 0 &&
        (Interlocked.Read(ref _lockedUntilTicks) == 0 || Interlocked.Read(ref _lockedUntilTicks) > DateTimeOffset.UtcNow.UtcTicks);

    public Task StopAsync()
    {
        lock (_gate)
            return _stopping ??= StopCoreAsync();
    }

    private async Task StopCoreAsync()
    {
        _stop.Cancel();
        await _renewal.ConfigureAwait(false);
        _stop.Dispose();
    }

    private async Task RenewAsync(TimeSpan maxDuration, Func<CancellationToken, Task<DateTimeOffset>> renew)
    {
        try
        {
            while (!_stop.IsCancellationRequested && IsValid)
            {
                var budget = maxDuration - _elapsed.Elapsed;
                if (budget <= TimeSpan.Zero) return;

                var remainingLock = new DateTimeOffset(Interlocked.Read(ref _lockedUntilTicks), TimeSpan.Zero) - DateTimeOffset.UtcNow;
                var buffer = Math.Min(remainingLock.TotalMilliseconds / 2, 10000);
                var delay = TimeSpan.FromMilliseconds(Math.Max(1, remainingLock.TotalMilliseconds - buffer));
                if (delay >= budget) return;
                await Task.Delay(delay, _stop.Token).ConfigureAwait(false);

                budget = maxDuration - _elapsed.Elapsed;
                if (budget <= TimeSpan.Zero || !IsValid) return;

                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                attempt.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(budget.TotalMilliseconds, int.MaxValue)));
                try
                {
                    var lockedUntil = await renew(attempt.Token).ConfigureAwait(false);
                    Interlocked.Exchange(ref _lockedUntilTicks, lockedUntil.UtcTicks);
                }
                catch (OperationCanceledException) when (attempt.IsCancellationRequested)
                {
                    return;
                }
                catch (ServiceBusException exception) when (exception.IsTransient)
                {
                    s_logger.LogWarning(exception, "Transient failure renewing a Service Bus lock");
                    await Task.Delay(TimeSpan.FromMilliseconds(100), _stop.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested || _elapsed.Elapsed >= maxDuration)
        {
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref _lost, 1);
            s_logger.LogWarning(exception, "Service Bus lock renewal stopped after a failure");
        }
    }
}
