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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.NATS;

public class NatsMessageProducerFactory(
    INatsClient client,
    INatsJSContext jsContext,
    IEnumerable<NatsPublication> publications,
    InstrumentationOptions instrumentation) : IAmAMessageProducerFactory
{
    public Dictionary<ProducerKey, IAmAMessageProducer> Create()
    {
        return BrighterAsyncContext.Run(async () => await CreateAsync());
    }

    public async Task<Dictionary<ProducerKey, IAmAMessageProducer>> CreateAsync()
    {
        var publicationsByTopic = new Dictionary<ProducerKey, IAmAMessageProducer>();
        foreach (var publication in publications)
        {
            if (RoutingKey.IsNullOrEmpty(publication.Topic))
            {
                continue;
            }

            if (publication is NatsStreamPublication streamPublication)
            {
                await EnsureExistsAsync(streamPublication);
                publicationsByTopic[new ProducerKey(publication.Topic, publication.Type)] =
                    new NatsStreamMessageProducer(jsContext, streamPublication, instrumentation);
            }
            else
            {
                publicationsByTopic[new ProducerKey(publication.Topic, publication.Type)] =
                    new NatsMessageProducer(client, publication, instrumentation);
            }
        }

        return publicationsByTopic;
    }


    private async Task EnsureExistsAsync(NatsStreamPublication publication)
    {
        if (publication.MakeChannels == OnMissingChannel.Assume)
        {
            return;
        }

        if (publication.MakeChannels == OnMissingChannel.Validate)
        {
            try
            {
                _ = await jsContext.GetStreamAsync(publication.Topic!.Value);
            }
            catch (NatsJSApiException e) when (e.Error.Code == 404)
            {
                throw new Exception();
            }

            return;
        }

        var config = publication.StreamConfiguration ?? new StreamConfig();
        await jsContext.CreateOrUpdateStreamAsync(config);
    }
}
