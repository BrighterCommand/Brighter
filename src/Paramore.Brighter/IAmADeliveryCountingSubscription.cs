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

namespace Paramore.Brighter;

/// <summary>
/// Identifies a subscription type whose delivery count can be tracked by a native broker counter,
/// exposing the native redrive limit and any reason the delivery budget is unenforceable (ADR 0077).
/// </summary>
/// <remarks>
/// Implemented by subscriptions on transports that re-serve their own stored copy on redelivery
/// (SQS, Pub/Sub, RocketMQ). The interface carries two budget-rule inputs consumed by startup
/// pipeline validation (<c>ConsumerValidationRules</c> in <c>Paramore.Brighter.ServiceActivator</c>):
/// <list type="bullet">
///   <item><description>
///     R-10 — <see cref="NativeRedriveLimit"/>: when non-null and the configured budget is at or
///     above this value, Brighter warns that the native limit will fire first.
///   </description></item>
///   <item><description>
///     R-11 — <see cref="DeliveryBudgetUnenforceableReason"/>: when non-null and a budget is
///     configured, Brighter warns that the budget cannot run down.
///   </description></item>
/// </list>
/// Subscriptions that do not implement this interface pass both R-10 and R-11 vacuously (R-22).
/// </remarks>
public interface IAmADeliveryCountingSubscription
{
    /// <summary>
    /// Gets the native redrive limit configured on this subscription's channel, if visible to Brighter.
    /// </summary>
    /// <value>
    /// The maximum number of deliveries the broker's own redrive policy allows (<c>M</c>), as a
    /// <see cref="int"/>; or <c>null</c> when no native redrive policy is configured or the limit
    /// is not visible to Brighter (e.g. the policy was provisioned outside Brighter).
    /// </value>
    int? NativeRedriveLimit { get; }

    /// <summary>
    /// Gets the reason the delivery budget is unenforceable for this subscription, if applicable.
    /// </summary>
    /// <value>
    /// A human-readable explanation of why the broker's delivery counter cannot advance for this
    /// subscription (e.g. a required <c>DeadLetterPolicy</c> is absent), so the configured
    /// <see cref="Subscription.RequeueCount"/> budget cannot run down; or <c>null</c> when
    /// R-1 holds for this subscription and the budget is enforceable.
    /// </value>
    string? DeliveryBudgetUnenforceableReason { get; }
}
