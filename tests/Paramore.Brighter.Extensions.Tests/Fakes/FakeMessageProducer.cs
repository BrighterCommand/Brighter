using System.Diagnostics;


namespace Paramore.Brighter.Extensions.Tests.Fakes
{
    internal class FakeMessageProducer : IAmAMessageProducer
    {
        public Publication Publication => new Publication()
        {
            Topic = new RoutingKey("greeting.event")
        };

        public Activity? Span { get; set; }
        public IAmAMessageScheduler? Scheduler { get; set; }
    }
}
