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
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using RabbitMQ.Client;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway;

/// <summary>
/// Declares a queue directly, so that a test can stand up the infrastructure its gateway expects to find.
/// </summary>
/// <remarks>
/// <para><paramref name="isDurable"/> defaults to true to match <see cref="RmqSubscription"/>. RabbitMQ 4.3
/// refuses to declare a transient, non-exclusive queue - the <c>transient_nonexcl_queues</c> feature is
/// deprecated - so a queue this factory creates for a gateway to read has to be durable to exist at all.</para>
/// <para>A queue cannot be redeclared under a different durability, so a caller that opts out here must pass
/// the same value to the consumer or subscription that reads the queue, or the redeclare fails with
/// <c>PRECONDITION_FAILED</c>.</para>
/// </remarks>
internal sealed class QueueFactory(RmqMessagingGatewayConnection connection, ChannelName channelName, RoutingKeys routingKeys, bool isDurable = true, QueueType queueType = QueueType.Classic)
{
    public async Task CreateAsync()
    {
        var connectionFactory = new ConnectionFactory { Uri = connection.AmpqUri.Uri };
        await using var connection1 = await connectionFactory.CreateConnectionAsync();
        await using var channel =
            await connection1.CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true));

        await channel.DeclareExchangeForConnection(connection, OnMissingChannel.Create);
        
        // Create arguments for queue declaration
        var arguments = new Dictionary<string, object?>();
        
        // Set queue type for quorum queues
        if (queueType == QueueType.Quorum)
        {
            arguments.Add("x-queue-type", "quorum");
        }
        
        await channel.QueueDeclareAsync(channelName.Value, isDurable, false, false, arguments.Any() ? arguments : null);
        if (routingKeys.Any())
        {
            foreach (RoutingKey routingKey in routingKeys)
            {
                await channel.QueueBindAsync(channelName.Value, connection.Exchange.Name, routingKey);
            }
        }
        else
        {
            await channel.QueueBindAsync(channelName.Value, connection.Exchange.Name, channelName!);
        }
    }
}
