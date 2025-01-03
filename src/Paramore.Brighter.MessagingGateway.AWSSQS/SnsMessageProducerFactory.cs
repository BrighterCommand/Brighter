#region Licence

/* The MIT License (MIT)
Copyright © 2024 Dominic Hickie <dominichickie@gmail.com>

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
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

public class SnsMessageProducerFactory : IAmAMessageProducerFactory
{
    private readonly AWSMessagingGatewayConnection _connection;
    private readonly IEnumerable<SnsPublication> _publications;

    /// <summary>
    /// Creates a collection of SNS message producers from the SNS publication information
    /// </summary>
    /// <param name="connection">The Connection to use to connect to AWS</param>
    /// <param name="publications">The publications describing the SNS topics that we want to use</param>
    public SnsMessageProducerFactory(
        AWSMessagingGatewayConnection connection,
        IEnumerable<SnsPublication> publications)
    {
        _connection = connection;
        _publications = publications;
    }

    /// <inheritdoc />
    /// <remarks>
    ///  Sync over async used here, alright in the context of producer creation
    /// </remarks>
    public Dictionary<RoutingKey, IAmAMessageProducer> Create()
    {
        var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();
        foreach (var p in _publications)
        {
            if (p.Topic is null)
            {
                throw new ConfigurationException("Missing topic on Publication");
            }

            var producer = new SnsMessageProducer(_connection, p);
            if (producer.ConfirmTopicExists())
            {
                producers[p.Topic] = producer;
            }
            else
            {
                throw new ConfigurationException($"Missing SNS topic: {p.Topic}");
            }
        }

        return producers;
    }

    /// <inheritdoc />
    public async Task<Dictionary<RoutingKey, IAmAMessageProducer>> CreateAsync()
    {
        var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();
        foreach (var p in _publications)
        {
            if (p.Topic is null)
            {
                throw new ConfigurationException("Missing topic on Publication");
            }

            var producer = new SnsMessageProducer(_connection, p);
            if (await producer.ConfirmTopicExistsAsync())
            {
                producers[p.Topic] = producer;
            }
            else
            {
                throw new ConfigurationException($"Missing SNS topic: {p.Topic}");
            }
        }

        return producers;
    }
}
