using GreetingsEntities;
using GreetingsPorts.EntityGateway.Interfaces;
using GreetingsPorts.Requests;
using Paramore.Brighter;

namespace GreetingsPorts.Handlers
{
    public class AddPersonHandlerAsync : RequestHandlerAsync<AddPerson>
    {
        private readonly IGreetingsEntityGateway unitOfWork;

        public AddPersonHandlerAsync(IGreetingsEntityGateway unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        public override async Task<AddPerson> HandleAsync(AddPerson command, CancellationToken cancellationToken = default)
        {
            using (unitOfWork)
            {
                unitOfWork.AddPerson(new Person(command.Name));
                await unitOfWork.CommitChanges();
            }

            return await base.HandleAsync(command, cancellationToken);
        }
    }
}
