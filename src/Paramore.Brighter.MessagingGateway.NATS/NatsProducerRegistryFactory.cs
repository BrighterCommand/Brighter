// The MIT License (MIT)
// Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.JetStream;
using NATS.Net;

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Creates a <see cref="ProducerRegistry"/> of NATS message producers from a set of publications.
/// </summary>
/// <param name="configuration">The <see cref="NatsMessageGatewayConfiguration"/> used to connect to NATS.</param>
/// <param name="publications">The <see cref="NatsPublication"/> set to create producers for.</param>
public class NatsProducerRegistryFactory(
    NatsMessageGatewayConfiguration configuration,
    IEnumerable<NatsPublication> publications) : IAmAProducerRegistryFactory
{
    /// <summary>
    /// Creates the producer registry, opening a NATS connection and a JetStream context.
    /// </summary>
    /// <returns>An <see cref="IAmAProducerRegistry"/> holding one producer per publication.</returns>
    /// <exception cref="ChannelFailureException">Thrown when a stream publication is validated but its JetStream stream does not exist.</exception>
    public IAmAProducerRegistry Create()
    {
        var natsClient = new NatsClient(configuration.NatsOpts);
        var jsClient = natsClient.CreateJetStreamContext(configuration.NatsJsOpts ?? new NatsJSOpts(configuration.NatsOpts));
        var producerFactory = new NatsMessageProducerFactory(natsClient, jsClient, publications, configuration.Instrumentation);

        return new ProducerRegistry(producerFactory.Create());
    }

    /// <summary>
    /// Creates the producer registry, opening a NATS connection and a JetStream context.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>An <see cref="IAmAProducerRegistry"/> holding one producer per publication.</returns>
    /// <exception cref="ChannelFailureException">Thrown when a stream publication is validated but its JetStream stream does not exist.</exception>
    public Task<IAmAProducerRegistry> CreateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Create());
    }
}
