using Paramore.Brighter;
using SalutationPorts.EntityGateway;
using SalutationPorts.Requests;

namespace SalutationPorts.Handlers
{
    public class GreetingMadeHandler : RequestHandlerAsync<GreetingMade>
    {
        private readonly SalutationsEntityGateway unitOfWork;

        public GreetingMadeHandler(SalutationsEntityGateway unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        public override Task<GreetingMade> HandleAsync(GreetingMade command, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Hello World!");
            return base.HandleAsync(command, cancellationToken);
        }
    }
}
