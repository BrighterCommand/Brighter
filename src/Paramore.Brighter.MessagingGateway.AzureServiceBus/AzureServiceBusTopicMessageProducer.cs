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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// A Sync and Async Message Producer for Azure Service Bus.
/// </summary>
public partial class AzureServiceBusTopicMessageProducer : AzureServiceBusMessageProducer
{
    protected override ILogger Logger => s_logger;
        
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AzureServiceBusTopicMessageProducer>();
        
    private readonly IAdministrationClientWrapper _administrationClientWrapper;

    /// <summary>
    /// An Azure Service Bus Message producer <see cref="IAmAMessageProducer"/>
    /// </summary>
    /// <param name="administrationClientWrapper">The administrative client.</param>
    /// <param name="serviceBusSenderProvider">The provider to use when producing messages.</param>
    /// <param name="publication">Configuration of a producer</param>
    /// <param name="bulkSendBatchSize">When sending more than one message using the MessageProducer, the max amount to send in a single transmission.</param>
    public AzureServiceBusTopicMessageProducer(
        IAdministrationClientWrapper administrationClientWrapper,
        IServiceBusSenderProvider serviceBusSenderProvider,
        AzureServiceBusPublication publication,
        int bulkSendBatchSize = 10
    ) : base(serviceBusSenderProvider, publication, bulkSendBatchSize)
    {
        _administrationClientWrapper = administrationClientWrapper;
    }

    protected override async Task EnsureChannelExistsAsync(string channelName)
    {
        if (TopicCreated || Publication.MakeChannels.Equals(OnMissingChannel.Assume))
            return;

        try
        {
            if (await _administrationClientWrapper.TopicExistsAsync(channelName))
            {
                TopicCreated = true;
                return;
            }

            if (Publication.MakeChannels.Equals(OnMissingChannel.Validate))
            {
                throw new ChannelFailureException($"Topic {channelName} does not exist and missing channel mode set to Validate.");
            }
                
            await _administrationClientWrapper.CreateTopicAsync(channelName);
            TopicCreated = true;
        }
        catch (Exception e)
        {
            //The connection to Azure Service bus may have failed so we re-establish the connection.
            _administrationClientWrapper.Reset();
            Log.FailingToCheckOrCreateTopic(s_logger, e);
            throw;
        }
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Error, "Failing to check or create topic")]
        public static partial void FailingToCheckOrCreateTopic(ILogger logger, Exception e);
    }
}

