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

using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter;

/// <summary>
/// Logs a Warning once at channel creation when a subscription's delivery budget is
/// unenforceable — that is, when <see cref="IAmADeliveryCountingSubscription.DeliveryBudgetUnenforceableReason"/>
/// is non-null and the budget is enabled (<c>RequeueCount != -1</c>) (R-11, R-26, ADR 0077).
/// </summary>
/// <remarks>
/// Called by each in-scope channel factory on every non-delegating creation path so the warning
/// appears once per channel, never on the receive path (NFR-4). Pipeline validation
/// (<c>ConsumerValidationRules.UnenforceableBudget</c>) also fires R-11 at startup; both routes
/// are required by R-26. R-7 and R-10 are pipeline-validation-only concerns and are never logged
/// here.
/// </remarks>
public static partial class DeliveryBudgetDiagnostics
{
    private static readonly ILogger s_logger =
        ApplicationLogging.LoggerFactory.CreateLogger(typeof(DeliveryBudgetDiagnostics));

    /// <summary>
    /// Logs a Warning when <paramref name="subscription"/> implements
    /// <see cref="IAmADeliveryCountingSubscription"/> and its delivery budget cannot run down.
    /// </summary>
    /// <param name="subscription">
    /// The <see cref="Subscription"/> being used to create a channel. Passes silently when
    /// the subscription does not implement <see cref="IAmADeliveryCountingSubscription"/>,
    /// when <see cref="Subscription.RequeueCount"/> is <c>-1</c> (budget disabled), or when
    /// <see cref="IAmADeliveryCountingSubscription.DeliveryBudgetUnenforceableReason"/> is <c>null</c>.
    /// </param>
    public static void WarnIfUnenforceable(Subscription subscription)
    {
        if (subscription is not IAmADeliveryCountingSubscription counting) return;
        if (subscription.RequeueCount == -1) return;
        if (counting.DeliveryBudgetUnenforceableReason is not { } reason) return;

        Log.UnenforceableDeliveryBudget(s_logger, subscription.Name, subscription.RequeueCount, reason);
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning,
            "Subscription '{SubscriptionName}' has requeueCount {RequeueCount} but the delivery count " +
            "cannot advance: {Reason}. The budget will not run down.")]
        public static partial void UnenforceableDeliveryBudget(
            ILogger logger,
            SubscriptionName subscriptionName,
            int requeueCount,
            string reason);
    }
}
