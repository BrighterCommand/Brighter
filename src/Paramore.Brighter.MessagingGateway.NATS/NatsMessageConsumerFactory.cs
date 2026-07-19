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
using System.Threading.Tasks;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Paramore.Brighter.MessagingGateway.NATS;

public class NatsMessageConsumerFactory(INatsClient natsClient, INatsJSContext natsJsContext) : IAmAMessageConsumerFactory
{
    public IAmAMessageConsumerSync Create(Subscription subscription)
    {
        if (subscription is NatsSubscription natsSubscription)
        {
            return Create(natsSubscription);
        }

        if (subscription is NatsStreamSubscription natsStreamSubscription)
        {
            return CreateAsync(natsStreamSubscription)
                .GetAwaiter()
                .GetResult();
        }

        throw new Exception();
    }

    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
    {
        if (subscription is NatsSubscription natsSubscription)
        {
            return Create(natsSubscription);
        }

        if (subscription is NatsStreamSubscription natsStreamSubscription)
        {
            return CreateAsync(natsStreamSubscription)
                .GetAwaiter()
                .GetResult();
        }

        throw new Exception();
    }

    private NatsMessageConsumer Create(NatsSubscription subscription)
    {
        var connection = natsClient.Connection;
        var serializer = natsClient.Connection.Opts.SerializerRegistry.GetDeserializer<byte[]>();

        var sub = new NatsSub<byte[]>(connection,
            connection.SubscriptionManager,
            subscription.ChannelName.Value,
            subscription.QueueGroup,
            subscription.NatsSubOpts,
            serializer);

        return new NatsMessageConsumer(sub, connection);
    }

    private async Task<NatsStreamMessageConsumer> CreateAsync(NatsStreamSubscription subscription)
    {
        await EnsureExistsAsync(subscription);
        var stream = await natsJsContext.GetStreamAsync(subscription.Consumer);
        var consumer = await stream.GetConsumerAsync(subscription.Consumer);
        var buffer = consumer.FetchAsync<byte[]>(new NatsJSFetchOpts
        {
            MaxMsgs = subscription.BufferSize,
            IdleHeartbeat = subscription.IdleHeartbeat,
            PriorityGroup = subscription.PriorityGroup,
        });

        return new NatsStreamMessageConsumer(stream, buffer);
    }

    private async Task EnsureExistsAsync(NatsStreamSubscription subscription)
    {
        if (subscription.MakeChannels == OnMissingChannel.Assume)
        {
            return;
        }

        if (subscription.MakeChannels == OnMissingChannel.Validate)
        {
            try
            {
                var s = await natsJsContext.GetStreamAsync(subscription.ChannelName.Value);
                _ = await s.GetConsumerAsync(subscription.Consumer); 
            }
            catch (NatsJSApiException e) when (e.Error.Code == 404)
            {
                throw new ChannelFailureException($"Stream {subscription.ChannelName.Value} does not exist", e);    
            }
        }

        var config = subscription.StreamConfiguration  ?? new StreamConfig();
        var stream = await natsJsContext.CreateOrUpdateStreamAsync(config);
        
        if(subscription.Ordered)
        {
            await stream.CreateOrderedConsumerAsync(subscription.OrderedConsumerOption);
        }
        else
        {
            await stream.CreateOrUpdateConsumerAsync(subscription.ConsumerOption);
        }
    }
}
