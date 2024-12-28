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


using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

public class AzureServiceBusProducerRegistryFactory : IAmAProducerRegistryFactory
{
    private readonly IServiceBusClientProvider _clientProvider;
    private readonly IEnumerable<AzureServiceBusPublication> _asbPublications;
    private readonly int _bulkSendBatchSize;

    /// <summary>
    /// Creates a producer registry initialized with producers for ASB derived from the publications
    /// </summary>
    /// <param name="configuration">The configuration of the connection to ASB</param>
    /// <param name="asbPublications">A set of publications - topics on the server - to configure</param>
    public AzureServiceBusProducerRegistryFactory(
        AzureServiceBusConfiguration configuration, 
        IEnumerable<AzureServiceBusPublication> asbPublications)
    {
        _clientProvider = new ServiceBusConnectionStringClientProvider(configuration.ConnectionString);
        _asbPublications = asbPublications;
        _bulkSendBatchSize = configuration.BulkSendBatchSize;
    }

    /// <summary>
    /// Creates a producer registry initialized with producers for ASB derived from the publications
    /// </summary>
    /// <param name="clientProvider">The connection to ASB</param>
    /// <param name="asbPublications">A set of publications - topics on the server - to configure</param>
    /// <param name="bulkSendBatchSize">The maximum size to chunk messages when dispatching to ASB</param>
    public AzureServiceBusProducerRegistryFactory(
        IServiceBusClientProvider clientProvider,
        IEnumerable<AzureServiceBusPublication> asbPublications,
        int bulkSendBatchSize = 10)
    {
        _clientProvider = clientProvider;
        _asbPublications = asbPublications;
        _bulkSendBatchSize = bulkSendBatchSize;
    }

    /// <summary>
    /// Creates message producers.
    /// </summary>
    /// <returns>A has of middleware clients by topic, for sending messages to the middleware</returns>
    public IAmAProducerRegistry Create()
    {
        var producerFactory = new AzureServiceBusMessageProducerFactory(_clientProvider, _asbPublications, _bulkSendBatchSize);

        return new ProducerRegistry(producerFactory.Create());
    }

    /// <summary>
    /// Creates message producers.
    /// </summary>
    /// <returns>A has of middleware clients by topic, for sending messages to the middleware</returns>
    public Task<IAmAProducerRegistry> CreateAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Create());
    }
}
