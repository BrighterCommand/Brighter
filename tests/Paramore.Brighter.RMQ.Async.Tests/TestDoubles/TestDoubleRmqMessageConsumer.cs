#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
/*
 * Use to force a failure mirroring a RabbitMQ subscription failure for testing flow of failure
 */

internal sealed class BrokerUnreachableRmqMessageConsumer : RmqMessageConsumer
{
    public BrokerUnreachableRmqMessageConsumer(RmqMessagingGatewayConnection connection, ChannelName queueName, RoutingKey routingKey, bool isDurable, ushort preFetchSize, bool isHighAvailability) 
        : base(connection, queueName, routingKey, isDurable, isHighAvailability) { }

    protected override Task EnsureChannelAsync(CancellationToken ct = default)
    {
        throw new BrokerUnreachableException(new Exception("Force Test Failure"));
    }
}

internal sealed class AlreadyClosedRmqMessageConsumer : RmqMessageConsumer
{
    public AlreadyClosedRmqMessageConsumer(RmqMessagingGatewayConnection connection, ChannelName queueName, RoutingKey routingKey, bool isDurable, ushort preFetchSize, bool isHighAvailability) 
        : base(connection, queueName, routingKey, isDurable, isHighAvailability) { }

    protected override Task EnsureChannelAsync(CancellationToken ct = default)
    {
        throw new AlreadyClosedException(new ShutdownEventArgs(ShutdownInitiator.Application, 0, "test"));
    }
}

internal sealed class OperationInterruptedRmqMessageConsumer : RmqMessageConsumer
{
    public OperationInterruptedRmqMessageConsumer(RmqMessagingGatewayConnection connection, ChannelName queueName, RoutingKey routingKey, bool isDurable, ushort preFetchSize, bool isHighAvailability) 
        : base(connection, queueName, routingKey, isDurable,isHighAvailability) { }

    protected override Task EnsureChannelAsync(CancellationToken ct = default)
    {
        throw new OperationInterruptedException(new ShutdownEventArgs(ShutdownInitiator.Application, 0, "test"));
    }
}

internal sealed class NotSupportedRmqMessageConsumer : RmqMessageConsumer
{
    public NotSupportedRmqMessageConsumer(RmqMessagingGatewayConnection connection, ChannelName queueName, RoutingKey routingKey, bool isDurable, ushort preFetchSize, bool isHighAvailability) 
        : base(connection, queueName, routingKey, isDurable, isHighAvailability) { }

    protected override Task EnsureChannelAsync(CancellationToken ct = default)
    {
        throw new NotSupportedException();
    }
}
