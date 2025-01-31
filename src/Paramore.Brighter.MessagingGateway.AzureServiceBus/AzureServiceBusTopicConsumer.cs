#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// Implementation of <see cref="IAmAMessageConsumerSync"/> using Azure Service Bus for Transport.
/// </summary>
public class AzureServiceBusTopicConsumer : AzureServiceBusConsumer
{
    protected override ILogger Logger => s_logger;

    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AzureServiceBusTopicConsumer>();
    private bool _subscriptionCreated;
    private readonly string _subscriptionName;
    private readonly IServiceBusReceiverProvider _serviceBusReceiverProvider;
    protected override string SubscriptionName => _subscriptionName;

    /// <summary>
    /// Initializes an Instance of <see cref="AzureServiceBusQueueConsumer"/> for Service Bus Topics
    /// </summary>
    /// <param name="subscription">An Azure Service Bus Subscription.</param>
    /// <param name="messageProducer">An instance of the Messaging Producer used for Requeue.</param>
    /// <param name="administrationClientWrapper">An Instance of Administration Client Wrapper.</param>
    /// <param name="serviceBusReceiverProvider">An Instance of <see cref="ServiceBusReceiverProvider"/>.</param>
    public AzureServiceBusTopicConsumer(
        AzureServiceBusSubscription subscription,
        IAmAMessageProducer messageProducer,
        IAdministrationClientWrapper administrationClientWrapper,
        IServiceBusReceiverProvider serviceBusReceiverProvider) 
        : base(subscription, messageProducer, administrationClientWrapper)
    {
        _subscriptionName = subscription.ChannelName.Value;
        _serviceBusReceiverProvider = serviceBusReceiverProvider;
    }
    
    /// <summary>
    /// Purges the specified queue name.
    /// </summary>
    public override async Task PurgeAsync(CancellationToken ct = default)
    {
        Logger.LogInformation("Purging messages from {Subscription} Subscription on Topic {Topic}",
            SubscriptionName, Topic);

        await AdministrationClientWrapper.DeleteTopicAsync(Topic);
        await EnsureChannelAsync();
    }
 
    protected override async Task EnsureChannelAsync()
    {
        if (_subscriptionCreated || Subscription.MakeChannels.Equals(OnMissingChannel.Assume))
            return;

        try
        {
            if (await AdministrationClientWrapper.SubscriptionExistsAsync(Topic, _subscriptionName))
            {
                _subscriptionCreated = true;
                return;
            }

            if (Subscription.MakeChannels.Equals(OnMissingChannel.Validate))
            {
                throw new ChannelFailureException(
                    $"Subscription {_subscriptionName} does not exist on topic {Topic} and missing channel mode set to Validate.");
            }

            await AdministrationClientWrapper.CreateSubscriptionAsync(Topic, _subscriptionName, SubscriptionConfiguration);
            _subscriptionCreated = true;
        }
        catch (ServiceBusException ex)
        {
            if (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                s_logger.LogWarning(
                    "Message entity already exists with topic {Topic} and subscription {ChannelName}", Topic,
                    _subscriptionName);
                _subscriptionCreated = true;
            }
            else
            {
                throw new ChannelFailureException("Failing to check or create subscription", ex);
            }
        }
        catch (Exception e)
        {
            s_logger.LogError(e, "Failing to check or create subscription");

            //The connection to Azure Service bus may have failed so we re-establish the connection.
            AdministrationClientWrapper.Reset();

            throw new ChannelFailureException("Failing to check or create subscription", e);
        }
    }
        
    protected override async Task GetMessageReceiverProviderAsync()
    {
        s_logger.LogInformation(
            "Getting message receiver provider for topic {Topic} and subscription {ChannelName}...",
            Topic, _subscriptionName);
        try
        {
            ServiceBusReceiver = await _serviceBusReceiverProvider.GetAsync(Topic, _subscriptionName,
                SubscriptionConfiguration.RequireSession);
        }
        catch (Exception e)
        {
            s_logger.LogError(e,
                "Failed to get message receiver provider for topic {Topic} and subscription {ChannelName}",
                Topic, _subscriptionName);
        }
    }
}
