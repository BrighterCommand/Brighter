using System.Transactions;
using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    public class AddPipeCommandHandler : RequestHandler<AddPipeCommand>
    {
        readonly IAmARepository<Pipe> pipeRepository;
        readonly IAmACommandProcessor commandProcessor;

        public AddPipeCommandHandler(IAmARepository<Pipe> pipeRepository, IAmACommandProcessor commandProcessor, ILog logger) : base(logger)
        {
            this.pipeRepository = pipeRepository;
            this.commandProcessor = commandProcessor;
        }

        #region Overrides of RequestHandler<AddPipeCommand>

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override AddPipeCommand Handle(AddPipeCommand command)
        {
            var pipe = new Pipe(new Identity(command.Id.ToString()), command.Type, new Title(command.Title));
            using (var scope = new TransactionScope())
            {
                pipeRepository.Add(pipe);
                scope.Complete();
            }

            commandProcessor.Send(new AddPipeToDomainCommand(command.DomainName, pipe.Name.Value));

            return base.Handle(command);
        }

        #endregion
    }
}
