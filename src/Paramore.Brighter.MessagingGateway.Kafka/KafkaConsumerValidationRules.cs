#region Licence

/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

namespace Paramore.Brighter.MessagingGateway.Kafka;

/// <summary>
/// Configuration-only startup validation rules for Kafka consumer subscriptions.
/// </summary>
public static class KafkaConsumerValidationRules
{
    /// <summary>
    /// Warns that the consumer group protocol does not report missing-topic subscription errors
    /// when <see cref="OnMissingChannel.Assume"/> skips infrastructure validation.
    /// </summary>
    /// <remarks>
    /// Register the returned specification as an <see cref="ISpecification{Subscription}"/>
    /// service before calling <c>ValidatePipelines()</c>. The rule reads the subscription's
    /// declared <see cref="KafkaSubscription.GroupProtocol"/>; it does not create a client,
    /// contact Kafka, or execute <see cref="KafkaSubscription.ConfigHook"/>. Protocol selection
    /// made by a custom <see cref="IGroupProtocol"/> or a configuration hook is not evaluated.
    /// </remarks>
    /// <returns>A specification that reports a non-blocking warning for Consumer protocol with Assume.</returns>
    public static ISpecification<Subscription> MissingTopicDetection()
        => new Specification<Subscription>(
            subscription => subscription is not KafkaSubscription
            {
                GroupProtocol: ConsumerGroupProtocol,
                MakeChannels: OnMissingChannel.Assume
            },
            subscription => new ValidationError(
                ValidationSeverity.Warning,
                $"Subscription '{subscription.Name}'",
                $"Topic '{subscription.RoutingKey}' uses KIP-848 with OnMissingChannel.Assume. " +
                "A missing topic will not produce a subscription error and may appear to be an empty channel. " +
                "Use OnMissingChannel.Validate if an infrastructure check is required and permitted. " +
                "This warning does not check whether the topic exists."));
}
