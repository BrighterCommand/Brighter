using System.Threading;
using System.Threading.Tasks;
using GreetingsEntities;
using GreetingsPorts.EntityGateway.Interfaces;
using GreetingsPorts.Requests;
using Paramore.Brighter;

namespace GreetingsPorts.Handlers
{
    public class AddGreetingHandlerAsync : RequestHandlerAsync<AddGreeting>
    {
        private readonly IGreetingsEntityGateway unitOfWork;
        private readonly IAmACommandProcessor commandProcessor;

        public AddGreetingHandlerAsync(IGreetingsEntityGateway unitOfWork, IAmACommandProcessor commandProcessor)
        {
            this.unitOfWork = unitOfWork;
            this.commandProcessor = commandProcessor;
        }

        public override async Task<AddGreeting> HandleAsync(AddGreeting command, CancellationToken cancellationToken = default)
        {
            using (unitOfWork)
            {
                var person = await unitOfWork.GetPersonByName(command.Name);
                var greeting = new Greeting
                {
                    Message = command.Greeting,
                };

                person.AddGreeting(greeting);
                await commandProcessor.PostAsync(new GreetingMade(greeting.Message));

                unitOfWork.UpdatePerson(person);

                await unitOfWork.CommitChanges();
            }

            return await base.HandleAsync(command, cancellationToken);
        }
    }
}
