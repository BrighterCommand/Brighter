#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using IServiceBusClientProvider = Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider.IServiceBusClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// Factory class for creating instances of <see cref="AzureServiceBusConsumer"/>
/// </summary>
public class AzureServiceBusConsumerFactory : IAmAMessageConsumerFactory
{
    private readonly IServiceBusClientProvider _clientProvider;
    private readonly AzureServiceBusPublication _publication;

    /// <summary>The scheduler used when the publication selects <see cref="RequeueSchedulerMode.Global"/>.</summary>
    public IAmAMessageScheduler? Scheduler { get; set; }

    /// <summary>
    /// Factory to create an Azure Service Bus Consumer
    /// </summary>
    /// <param name="configuration">The configuration to connect to <see cref="AzureServiceBusConfiguration"/></param>
    public AzureServiceBusConsumerFactory(AzureServiceBusConfiguration configuration)
        : this(new ServiceBusConnectionStringClientProvider(configuration.ConnectionString))
    { }

    /// <summary>
    /// Factory to create an Azure Service Bus Consumer
    /// </summary>
    /// <param name="clientProvider">A client Provider <see cref="IServiceBusClientProvider"/> to determine how to connect to ASB</param>
    public AzureServiceBusConsumerFactory(IServiceBusClientProvider clientProvider)
        : this(clientProvider, new AzureServiceBusPublication())
    {
    }

    /// <summary>Creates consumers using the publication's retry scheduler selection.</summary>
    /// <param name="clientProvider">The client for the consuming broker.</param>
    /// <param name="publication">The publication selecting native or global retries.</param>
    public AzureServiceBusConsumerFactory(IServiceBusClientProvider clientProvider, AzureServiceBusPublication publication)
    {
        _clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
        _publication = publication ?? throw new ArgumentNullException(nameof(publication));
    }

    /// <summary>
    /// Creates a consumer for the specified queue.
    /// </summary>
    /// <param name="subscription">The queue to connect to</param>
    /// <returns>IAmAMessageConsumerSync</returns>
    /// <exception cref="ConfigurationException">The subscription's retry, forwarding or lock renewal configuration is invalid.</exception>
    public IAmAMessageConsumerSync Create(Subscription subscription) => CreateConsumer(subscription, false);

    /// <summary>Creates an asynchronous consumer for the subscription.</summary>
    /// <param name="subscription">The subscription to consume.</param>
    /// <returns>The asynchronous consumer.</returns>
    /// <exception cref="ConfigurationException">The retry configuration is invalid.</exception>
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription) => CreateConsumer(subscription, true);

    private AzureServiceBusConsumer CreateConsumer(Subscription subscription, bool isAsync)
    {
        if (subscription is not AzureServiceBusSubscription sub)
            throw new ArgumentException("Subscription is not of type AzureServiceBusSubscription.", nameof(subscription));

        if (sub.Configuration.MaxAutoLockRenewalDuration < TimeSpan.Zero)
            throw new ConfigurationException("MaxAutoLockRenewalDuration must be zero or positive.");

        if (sub.Configuration.ForwardTo is { } queueName
            && (sub.Configuration.UseServiceBusQueue || !AzureServiceBusQueuePath.IsValid(queueName)))
        {
            throw new ConfigurationException(
                "ForwardTo must name a queue in the same namespace, not a subscription or subqueue, and cannot be combined with UseServiceBusQueue.");
        }

        var usesQueue = sub.Configuration.UseServiceBusQueue || sub.Configuration.ForwardTo is not null;
        if (usesQueue && !AzureServiceBusQueuePath.IsValid(sub.Configuration.ForwardTo ?? sub.RoutingKey.Value))
            throw new ConfigurationException("Retries require a physical queue name, not a subscription or subqueue path.");

        IAmAMessageScheduler? scheduler = _publication.RequeueScheduler switch
        {
            RequeueSchedulerMode.Native => usesQueue ? new AzureServiceBusRequeueScheduler(_clientProvider) : null,
            RequeueSchedulerMode.Global when !usesQueue => throw new ConfigurationException(
                "Global retry scheduling requires UseServiceBusQueue or ForwardTo to identify a physical queue."),
            RequeueSchedulerMode.Global => Scheduler,
            _ => throw new ConfigurationException("Unknown requeue scheduler mode.")
        };
        if (usesQueue && (isAsync
                ? scheduler is not IAmAMessageRequeueSchedulerAsync
                : scheduler is not IAmAMessageRequeueSchedulerSync))
        {
            throw new ConfigurationException(isAsync
                ? "The selected retry scheduler must implement IAmAMessageRequeueSchedulerAsync."
                : "The selected retry scheduler must implement IAmAMessageRequeueSchedulerSync.");
        }

        var administration = new AdministrationClientWrapper(_clientProvider);
        var receiverProvider = new ServiceBusReceiverProvider(_clientProvider, sub.Configuration.MaxAutoLockRenewalDuration);
        if (sub.Configuration.ForwardTo is not null)
            return new AzureServiceBusForwardingConsumer(sub, scheduler!, administration, receiverProvider);
        if (sub.Configuration.UseServiceBusQueue)
            return new AzureServiceBusQueueConsumer(sub, scheduler!, administration, receiverProvider);
        return new AzureServiceBusTopicConsumer(sub, administration, receiverProvider);
    }
}
