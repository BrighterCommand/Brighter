using System.Threading;
using System.Threading.Tasks;
using GreetingsEntities;
using GreetingsPorts.EntityGateway;
using GreetingsPorts.Requests;
using Paramore.Brighter;

namespace GreetingsPorts.Handlers
{
    public class AddPersonHandlerAsync : RequestHandlerAsync<AddPerson>
    {
        private readonly GreetingsEntityGateway unitOfWork;

        public AddPersonHandlerAsync(GreetingsEntityGateway unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        public override async Task<AddPerson> HandleAsync(AddPerson command, CancellationToken cancellationToken = default)
        {
            using (unitOfWork)
            {
                unitOfWork.Add(new Person(command.Name));
                await unitOfWork.CommitChanges();
            }

            return await base.HandleAsync(command, cancellationToken);
        }
    }
}
