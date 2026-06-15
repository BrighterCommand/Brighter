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
using Microsoft.Extensions.Logging;
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
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Factory to create a dictionary of Azure Service Bus Producers indexed by topic name
    /// </summary>
    /// <param name="clientProvider">The connection to ASB</param>
    /// <param name="publications">A set of publications - topics on the server - to configure</param>
    /// <param name="bulkSendBatchSize">The maximum size to chunk messages when dispatching to ASB</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> used to create loggers</param>
    public AzureServiceBusMessageProducerFactory(
        IServiceBusClientProvider clientProvider,
        IEnumerable<AzureServiceBusPublication> publications,
        int bulkSendBatchSize,
        ILoggerFactory? loggerFactory = null)
    {
        _clientProvider = clientProvider;
        _publications = publications;
        _bulkSendBatchSize = bulkSendBatchSize;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a dictionary of in-memory message producers.
    /// </summary>
    /// <returns>A dictionary of <see cref="IAmAMessageProducer"/> indexed by <see cref="RoutingKey"/></returns>
    /// <exception cref="ArgumentException">Thrown when a publication does not have a topic</exception>

    public Dictionary<ProducerKey, IAmAMessageProducer> Create()
    {
        var nameSpaceManagerWrapper = new AdministrationClientWrapper(_clientProvider, _loggerFactory);
        var topicClientProvider = new ServiceBusSenderProvider(_clientProvider);

        var producers = new Dictionary<ProducerKey, IAmAMessageProducer>();
        foreach (var publication in _publications)
        {
            if (publication.Topic is null)
                throw new ArgumentException("Publication must have a Topic.");

            if (publication.UseServiceBusQueue)
            {
                var producer = new AzureServiceBusQueueMessageProducer(nameSpaceManagerWrapper, topicClientProvider, publication, _bulkSendBatchSize, _loggerFactory);
                producer.Publication = publication;
                RegisterProducer(publication, producers, producer);
            }
            else
            {
                var producer = new AzureServiceBusTopicMessageProducer(nameSpaceManagerWrapper, topicClientProvider, publication, _bulkSendBatchSize, _loggerFactory);
                producer.Publication = publication;
                RegisterProducer(publication, producers, producer);

            }
        }

        return producers;
    }
    
    public Task<Dictionary<ProducerKey, IAmAMessageProducer>> CreateAsync()
    {
        return Task.FromResult(Create());
    }
       
    private static void RegisterProducer(AzureServiceBusPublication publication, Dictionary<ProducerKey, IAmAMessageProducer> producers, IAmAMessageProducer producer)
    {
        var producerKey = new ProducerKey(publication.Topic!, publication.Type);
        if (producers.ContainsKey(producerKey))
            throw new ArgumentException($"A publication with the topic {publication.Topic}  and {publication.Type} already exists in the producer registry. Each topic + type must be unique in the producer registry. If you did not set a type, we will match against an empty type, so you cannot have two publications with the same topic and no type in the producer registry.");
        producers[producerKey] = producer;
    }

   
}
