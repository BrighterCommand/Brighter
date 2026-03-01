using System;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Adaptors.Data;
using Greetings.Adaptors.Services;
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
        private readonly IUnitOfWork _unitOfWork;

        public AddGreetingCommandHandler(GreetingsDataContext dataContext, IAmACommandProcessor commandProcessor, IUnitOfWork unitOfWork)
        {
            _dataContext = dataContext;
            _commandProcessor = commandProcessor;
            _unitOfWork = unitOfWork;
        }
        
        public async override Task<AddGreetingCommand> HandleAsync(AddGreetingCommand command,
            CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                //Save  the new Greeting
                var greeting = new Greeting {GreetingMessage = command.GreetingMessage};
                await _dataContext.GreetingsRegister.AddAsync(greeting, cancellationToken);

                //Create an Event for externals
                var newGreetingAddedEvent = new GreetingAsyncEvent {Greeting = command.GreetingMessage};
                var eventId = await _commandProcessor.DepositPostAsync(newGreetingAddedEvent, cancellationToken: cancellationToken);

                await _dataContext.SaveChangesAsync(cancellationToken);

                if (command.ThrowError) throw new Exception("something broke error");
                else
                {
                    //Ensure for Testing to Ensure that Contexts are not shared
                    Thread.Sleep(5000);
                }

                await _unitOfWork.CommitAsync(cancellationToken);
                
                //In Case there is no outbox Sweeper
                await _commandProcessor.ClearOutboxAsync([eventId], cancellationToken: cancellationToken);
                
                Console.WriteLine($"Message {command.GreetingMessage} Saved.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                await _unitOfWork.RollbackAsync(cancellationToken);
                
                Console.WriteLine($"Message {command.GreetingMessage} not Saved.");
            }
            
            
            return await base.HandleAsync(command, cancellationToken);
        }
    }
}
