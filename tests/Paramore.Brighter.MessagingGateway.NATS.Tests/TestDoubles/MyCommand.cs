using Paramore.Brighter;

namespace Paramore.Brighter.MessagingGateway.NATS.Tests;

public class MyCommand : IRequest
{
    public int Id { get; set; }
}
