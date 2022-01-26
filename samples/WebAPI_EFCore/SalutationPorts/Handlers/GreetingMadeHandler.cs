using Paramore.Brighter;
using SalutationEntities;
using SalutationPorts.Requests;

namespace SalutationPorts.Handlers
{
    public class GreetingMadeHandler : RequestHandler<GreetingMade>
    {
        public override GreetingMade Handle(GreetingMade @event)
        {
            var salutation = new Salutation(@event.Greeting);
            return base.Handle(@event);
        }
    }
}
