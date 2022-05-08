using GreetingsPorts.EntityGateway.Interfaces;
using GreetingsPorts.Requests;
using Paramore.Brighter;

namespace GreetingsPorts.Handlers
{
    public class DeletePersonHandlerAsync : RequestHandlerAsync<DeletePerson>
    {
        private readonly IGreetingsEntityGateway unitOfWork;

        public DeletePersonHandlerAsync(IGreetingsEntityGateway unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        public override async Task<DeletePerson> HandleAsync(DeletePerson command, CancellationToken cancellationToken = default)
        {
            using (unitOfWork)
            {
                var personToDelete = await unitOfWork.GetPersonByName(command.Name);

                unitOfWork.DeletePerson(personToDelete.Id);
                await unitOfWork.CommitChanges();
            }

            return await base.HandleAsync(command, cancellationToken);
        }
    }
}
