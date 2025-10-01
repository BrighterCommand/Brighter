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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// The SQS Message Producer registry factory
/// </summary>
public class SqsProducerRegistryFactory : IAmAProducerRegistryFactory
{
    private readonly AWSMessagingGatewayConnection _connection;
    private readonly IEnumerable<SqsPublication> _sqsPublications;

    /// <summary>
    /// Create a collection of producers from the publication information
    /// </summary>
    /// <param name="connection">The Connection to use to connect to AWS</param>
    /// <param name="sqsPublications">The publication describing the SNS topic that we want to use</param>
    public SqsProducerRegistryFactory(
        AWSMessagingGatewayConnection connection,
        IEnumerable<SqsPublication> sqsPublications)
    {
        _connection = connection;
        _sqsPublications = sqsPublications;
    }

    /// <summary>
    /// Create a message producer for each publication, add it into the registry under the key of the topic
    /// </summary>
    /// <returns>The <see cref="ProducerRegistry"/> with <see cref="SnsMessageProducerFactory"/>.</returns>
    public IAmAProducerRegistry Create()
    {
        var producerFactory = new SqsMessageProducerFactory(_connection, _sqsPublications);
        return new ProducerRegistry(producerFactory.Create());
    }

    /// <summary>
    /// Create a message producer for each publication, add it into the registry under the key of the topic
    /// </summary>
    /// <param name="ct">The <see cref="CancellationToken"/>.</param>
    /// <returns>The <see cref="ProducerRegistry"/> with <see cref="SnsMessageProducerFactory"/>.</returns>
    public async Task<IAmAProducerRegistry> CreateAsync(CancellationToken ct = default)
    {
        var producerFactory = new SqsMessageProducerFactory(_connection, _sqsPublications);
        return new ProducerRegistry(await producerFactory.CreateAsync());
    }
}
