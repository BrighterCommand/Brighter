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
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// Factory class for creating dictionary of instances of <see cref="AzureServiceBusMessageProducer"/>
/// indexed by topic name
/// </summary>
public class AzureServiceBusMessageProducerFactory : IAmAMessageProducerFactory
{
    private readonly IServiceBusClientProvider _clientProvider;
    private readonly IEnumerable<AzureServiceBusPublication> _publications;
    private readonly int _bulkSendBatchSize;

    /// <summary>
    /// Factory to create a dictionary of Azure Service Bus Producers indexed by topic name
    /// </summary>
    /// <param name="clientProvider">The connection to ASB</param>
    /// <param name="publications">A set of publications - topics on the server - to configure</param>
    /// <param name="bulkSendBatchSize">The maximum size to chunk messages when dispatching to ASB</param>
    public AzureServiceBusMessageProducerFactory(
        IServiceBusClientProvider clientProvider,
        IEnumerable<AzureServiceBusPublication> publications,
        int bulkSendBatchSize)
    {
        _clientProvider = clientProvider;
        _publications = publications;
        _bulkSendBatchSize = bulkSendBatchSize;
    }

    /// <inheritdoc />
    public Dictionary<RoutingKey, IAmAMessageProducer> Create()
    {
        var nameSpaceManagerWrapper = new AdministrationClientWrapper(_clientProvider);
        var topicClientProvider = new ServiceBusSenderProvider(_clientProvider);

        var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();
        foreach (var publication in _publications)
        {
            if (publication.Topic is null)
                throw new ArgumentException("Publication must have a Topic.");
            if(publication.UseServiceBusQueue)
                producers.Add(publication.Topic, new AzureServiceBusQueueMessageProducer(nameSpaceManagerWrapper, topicClientProvider, publication, _bulkSendBatchSize));
            else
                producers.Add(publication.Topic, new AzureServiceBusTopicMessageProducer(nameSpaceManagerWrapper, topicClientProvider, publication, _bulkSendBatchSize));
        }

        return producers;
    }

    public Task<Dictionary<RoutingKey, IAmAMessageProducer>> CreateAsync()
    {
        return Task.FromResult(Create());
    }
}
