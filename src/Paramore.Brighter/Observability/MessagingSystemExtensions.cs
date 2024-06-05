#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.Observability;

/// <summary>
/// Provide a helper method to turn span attributes into strings (lowercase)
/// </summary>
public static class MessagingSystemExtensions
{
    ///<summary>
    /// Provide a string representation of the messaging system
    /// </summary>
    public static string ToMessagingSystemName(this MessagingSystem messagingSystem) => messagingSystem switch
    {
        MessagingSystem.ActiveMQ => "activemq",
        MessagingSystem.AWSSQS => "aws_sqs",
        MessagingSystem.EventGrid => "eventgrid",
        MessagingSystem.EventHubs => "eventhubs",
        MessagingSystem.InternalBus => "internal_bus",
        MessagingSystem.JMS => "jms",
        MessagingSystem.Kafka => "kafka",
        MessagingSystem.PubSub => "gcp_pubsub",
        MessagingSystem.RabbitMQ => "rabbitmq",
        MessagingSystem.RocketMQ => "rocketmq",
        MessagingSystem.ServiceBus => "servicebus",
        _ => throw new ArgumentOutOfRangeException(nameof(messagingSystem), messagingSystem, null)
    };
}
