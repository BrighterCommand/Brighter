using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GreetingsPorts.EntityGateway;
using GreetingsPorts.Requests;
using Paramore.Brighter;

namespace GreetingsPorts.Handlers
{
    public class AddPersonHandlerAsync : RequestHandlerAsync<AddPerson>
    {
        private readonly GreetingsEntityGateway _uow;
        private readonly IAmACommandProcessor _commandProcessor;

        public AddPersonHandlerAsync(GreetingsEntityGateway uow, IAmACommandProcessor commandProcessor)
        {
            _uow = uow;
            _commandProcessor = commandProcessor;
        }

        public override async Task<AddPerson> HandleAsync(AddPerson command, CancellationToken cancellationToken = default)
        {
            // _uow.Add(new Person(command.Name));
            // await savechangesasync
            Console.WriteLine("Hello World!");
            return await base.HandleAsync(command, cancellationToken);
        }
    }
}
