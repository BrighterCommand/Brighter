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

using Confluent.Kafka;

namespace Paramore.Brighter.MessagingGateway.Kafka;

/// <summary>
/// Configures Kafka's consumer group protocol for broker-driven assignment.
/// </summary>
/// <remarks>
/// With the consumer protocol (KIP-848), group membership is broker-driven and completes asynchronously:
/// subscribing to a non-existent topic does not fail synchronously at consumer creation. Infrastructure
/// validation via <see cref="OnMissingChannel.Validate"/> therefore has weaker guarantees than with the
/// classic protocol; prefer <see cref="OnMissingChannel.Create"/>, or provision topics out-of-band.
/// </remarks>
public class ConsumerGroupProtocol : IGroupProtocol
{
    /// <summary>
    /// Gets or sets the broker-side assignor name used when the consumer group protocol is <c>consumer</c>.
    /// </summary>
    /// <value>The assignor name as a <see cref="string"/>, or <see langword="null"/> to use broker defaults.</value>
    public string? GroupRemoteAssignor { get; set; }

    /// <summary>
    /// Gets or sets the static membership identifier for this consumer instance.
    /// </summary>
    /// <value>The static member identifier as a <see cref="string"/>, or <see langword="null"/> for dynamic membership.</value>
    /// <remarks>
    /// Kafka static membership requires a unique <c>group.instance.id</c> per consumer instance. The
    /// <see cref="ConsumerGroupProtocol"/> instance held by a subscription is applied to every consumer
    /// created from that subscription (one per performer), so all of those consumers would register the
    /// same static id and collide on group join. Only set this property when the subscription has a single
    /// performer; to use static membership with multiple performers, assign a unique
    /// <c>group.instance.id</c> per consumer via the <c>configHook</c> instead.
    /// </remarks>
    public string? GroupInstanceId { get; set; }
    
    /// <inheritdoc />
    public void Apply(ConsumerConfig config)
    {
        config.GroupProtocol = GroupProtocol.Consumer;
        config.GroupRemoteAssignor = GroupRemoteAssignor;
        config.GroupInstanceId = GroupInstanceId;
    }
}
