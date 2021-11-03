using System;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Adaptors.Data;
using Greetings.Ports.Commands;
using Greetings.Ports.Entities;
using Greetings.Ports.Events;
using Paramore.Brighter;

namespace Greetings.Ports.CommandHandlers
{
    public class AddGreetingCommandHandler : RequestHandlerAsync<AddGreetingCommand>
    {
        private readonly GreetingsDataContext _dataContext;
        private readonly IAmACommandProcessor _commandProcessor;

        public AddGreetingCommandHandler(GreetingsDataContext dataContext, IAmACommandProcessor commandProcessor)
        {
            _dataContext = dataContext;
            _commandProcessor = commandProcessor;
        }
        
        public async override Task<AddGreetingCommand> HandleAsync(AddGreetingCommand command,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var txn = await _dataContext.Database.BeginTransactionAsync();

            try
            {
                //Save  the new Greeting
                var greeting = new Greeting() {GreetingMessage = command.GreetingMessage};
                await _dataContext.GreetingsRegister.AddAsync(greeting, cancellationToken);

                //Create an Event for externals
                var newGreetingAddedEvent = new GreetingAsyncEvent() {Greeting = command.GreetingMessage};
                var eventId = await _commandProcessor.DepositPostAsync(newGreetingAddedEvent, cancellationToken: cancellationToken);
                
                if (command.ThrowError) throw new Exception("something broke error");

                await txn.CommitAsync(cancellationToken);
                
                //In Case there is no outbox Sweeper
                await _commandProcessor.ClearOutboxAsync(new[] {eventId}, cancellationToken: cancellationToken);
                
                Console.WriteLine($"Message {command.GreetingMessage} Saved.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                await txn.RollbackAsync(cancellationToken);
                
                Console.WriteLine($"Message {command.GreetingMessage} not Saved.");
            }
            
            
            return await base.HandleAsync(command);
        }
    }
}
