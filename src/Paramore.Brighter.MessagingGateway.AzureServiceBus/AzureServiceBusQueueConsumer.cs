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
public class AzureServiceBusQueueConsumer : AzureServiceBusConsumer
{
    protected override string SubscriptionName => "Queue";
    protected override ILogger Logger => s_logger;
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AzureServiceBusQueueConsumer>();
    private readonly IServiceBusReceiverProvider _serviceBusReceiverProvider;

    private bool _queueCreated = false;
        
    /// <summary>
    /// Initializes an Instance of <see cref="AzureServiceBusQueueConsumer"/> for Service Bus Queus
    /// </summary>
    /// <param name="subscription">An Azure Service Bus Subscription.</param>
    /// <param name="messageProducer">An instance of the Messaging Producer used for Requeue.</param>
    /// <param name="administrationClientWrapper">An Instance of Administration Client Wrapper.</param>
    /// <param name="serviceBusReceiverProvider">An Instance of <see cref="ServiceBusReceiverProvider"/>.</param>
    public AzureServiceBusQueueConsumer(AzureServiceBusSubscription subscription,
        IAmAMessageProducerSync messageProducer,
        IAdministrationClientWrapper administrationClientWrapper,
        IServiceBusReceiverProvider serviceBusReceiverProvider) : base(subscription,
        messageProducer, administrationClientWrapper)
    {
        _serviceBusReceiverProvider = serviceBusReceiverProvider;
    }

    protected override async Task GetMessageReceiverProviderAsync()
    {
        s_logger.LogInformation(
            "Getting message receiver provider for queue {Queue}...",
            Topic);
        try
        {
            ServiceBusReceiver = await _serviceBusReceiverProvider.GetAsync(Topic, SubscriptionConfiguration.RequireSession);
        }
        catch (Exception e)
        {
            s_logger.LogError(e, "Failed to get message receiver provider for queue {Queue}", Topic);
        }
    }
        
    /// <summary>
    /// Purges the specified queue name.
    /// </summary>
    public override async Task PurgeAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        Logger.LogInformation("Purging messages from Queue {Queue}", Topic);

        await AdministrationClientWrapper.DeleteQueueAsync(Topic);
        await EnsureChannelAsync();
    }

    protected override async Task EnsureChannelAsync()
    {
        if (_queueCreated || Subscription.MakeChannels.Equals(OnMissingChannel.Assume))
            return;

        try
        {
            if (await AdministrationClientWrapper.QueueExistsAsync(Topic))
            {
                _queueCreated = true;
                return;
            }

            if (Subscription.MakeChannels.Equals(OnMissingChannel.Validate))
            {
                throw new ChannelFailureException($"Queue {Topic} does not exist and missing channel mode set to Validate.");
            }

            await AdministrationClientWrapper.CreateQueueAsync(Topic, SubscriptionConfiguration.QueueIdleBeforeDelete);
            _queueCreated = true;
        }
        catch (ServiceBusException ex)
        {
            if (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                s_logger.LogWarning(
                    "Message entity already exists with queue {Queue}", Topic);
                _queueCreated = true;
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
}
