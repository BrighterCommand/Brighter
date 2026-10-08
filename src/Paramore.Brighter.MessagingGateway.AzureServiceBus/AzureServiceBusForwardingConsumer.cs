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
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

internal sealed class AzureServiceBusForwardingConsumer(
    AzureServiceBusSubscription subscription,
    IAmAMessageProducer messageProducer,
    IAdministrationClientWrapper administrationClientWrapper,
    IServiceBusReceiverProvider receiverProvider)
    : AzureServiceBusConsumer(subscription, messageProducer, administrationClientWrapper)
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AzureServiceBusForwardingConsumer>();
    private readonly string _queueName = subscription.Configuration.ForwardTo!;
    private bool _channelCreated;

    protected override ILogger Logger => s_logger;
    protected override string SubscriptionName => Subscription.ChannelName.Value;

    protected override async Task GetMessageReceiverProviderAsync()
        => ServiceBusReceiver = await receiverProvider.GetAsync(_queueName, SubscriptionConfiguration.RequireSession);

    public override async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Subscription.MakeChannels != OnMissingChannel.Create)
        {
            throw new NotSupportedException(
                "Purging a forwarding queue requires OnMissingChannel.Create so the destination can be recreated.");
        }

        await ResetReceiverAsync();
        _channelCreated = false;
        await AdministrationClientWrapper.DeleteQueueAsync(_queueName);
        await EnsureChannelAsync();
    }

    protected override async Task EnsureChannelAsync()
    {
        if (_channelCreated || Subscription.MakeChannels == OnMissingChannel.Assume)
            return;

        await EnsureQueueAsync();
        await EnsureSubscriptionAsync();
        _channelCreated = true;
    }

    private async Task EnsureQueueAsync()
    {
        if (await AdministrationClientWrapper.QueueExistsAsync(_queueName))
            return;

        if (Subscription.MakeChannels == OnMissingChannel.Validate)
            throw new ChannelFailureException($"Forwarding queue {_queueName} does not exist.");

        try
        {
            await AdministrationClientWrapper.CreateQueueAsync(_queueName, SubscriptionConfiguration);
        }
        catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Another consumer may provision the shared destination concurrently.
        }
    }

    private async Task EnsureSubscriptionAsync()
    {
        if (await AdministrationClientWrapper.SubscriptionExistsAsync(Topic, SubscriptionName))
        {
            await ValidateForwardingAsync();
            if (Subscription.MakeChannels == OnMissingChannel.Create && SubscriptionConfiguration.GetRuleOptions() is not null)
                await AdministrationClientWrapper.CreateSubscriptionAsync(Topic, SubscriptionName, SubscriptionConfiguration);

            return;
        }

        if (Subscription.MakeChannels == OnMissingChannel.Validate)
            throw new ChannelFailureException($"Subscription {SubscriptionName} does not exist on topic {Topic}.");

        await AdministrationClientWrapper.CreateSubscriptionAsync(Topic, SubscriptionName, SubscriptionConfiguration);
        await ValidateForwardingAsync();
    }

    private async Task ValidateForwardingAsync()
    {
        var properties = await AdministrationClientWrapper.GetSubscriptionAsync(Topic, SubscriptionName);
        var destination = properties.ForwardTo;
        if (Uri.TryCreate(destination, UriKind.Absolute, out var address))
            destination = Uri.UnescapeDataString(address.AbsolutePath).TrimStart('/');

        if (!string.Equals(destination, _queueName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ChannelFailureException(
                $"Subscription {SubscriptionName} on topic {Topic} must forward to queue {_queueName}. " +
                "Provision forwarding before starting this consumer; existing subscriptions are not reconfigured.");
        }
    }
}
