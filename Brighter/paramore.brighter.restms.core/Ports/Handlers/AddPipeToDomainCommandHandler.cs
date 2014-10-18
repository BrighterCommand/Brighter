using System.Transactions;
using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    public class AddPipeToDomainCommandHandler : RequestHandler<AddPipeToDomainCommand>
    {
        readonly IAmARepository<Domain> repository;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="logger">The logger.</param>
        public AddPipeToDomainCommandHandler(IAmARepository<Domain> repository, ILog logger) : base(logger)
        {
            this.repository = repository;
        }

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="addPipeToDomainCommand">The command.</param>
        /// <returns>TRequest.</returns>
        public override AddPipeToDomainCommand Handle(AddPipeToDomainCommand addPipeToDomainCommand)
        {
            using (var scope = new TransactionScope())
            {
                var domain = repository[new Identity(addPipeToDomainCommand.DomainName)];
                if (domain == null)
                {
                    throw new DomainNotFoundException();
                }

                domain.AddPipe(new Identity(addPipeToDomainCommand.PipeName));

                scope.Complete();
            }
            return base.Handle(addPipeToDomainCommand);
        }

    }
}
