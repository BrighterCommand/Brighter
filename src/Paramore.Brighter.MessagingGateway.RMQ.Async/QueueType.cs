#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

namespace Paramore.Brighter.MessagingGateway.RMQ.Async;

/// <summary>
/// RabbitMQ offers two types of queues: Classic and Quorum.
/// For more information see
/// https://www.rabbitmq.com/docs/quorum-queues
/// </summary>
public enum QueueType
{
    /// <summary>
    /// Classic queues are the traditional RabbitMQ queue type. They are designed for high throughput
    /// and low latency. Classic queues are ideal for use cases that require processing large volumes
    /// of messages quickly, such as real-time data streaming or large-scale applications.
    /// Classic queues support mirroring for high availability but do not provide the same consistency
    /// guarantees as quorum queues.
    /// </summary>
    Classic,

    /// <summary>
    /// Quorum queues are designed to provide high availability and data consistency. They use the Raft
    /// consensus algorithm to ensure that messages are replicated across multiple nodes in the cluster.
    /// Quorum queues are ideal for use cases where message durability and consistency are critical,
    /// such as financial transactions or critical business processes. 
    /// Quorum queues require at least 3 nodes for optimal performance and require durability to be enabled.
    /// They do not support high availability mirroring as they provide their own replication mechanism.
    /// </summary>
    Quorum
}