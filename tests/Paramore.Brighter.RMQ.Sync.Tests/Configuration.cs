using System;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;

namespace Paramore.Brighter.RMQ.Sync.Tests;

public static class Configuration
{
    public static RmqMessagingGatewayConnection CreateConnection()
    {
        return new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange", supportDelay: false)
        };
    }
    
    public static RmqMessagingGatewayConnection PersistConnection { get; } = new()
    {
        AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
        Exchange = new Exchange("paramore.brighter.exchange", supportDelay: false),
        PersistMessages = true
    };
}
