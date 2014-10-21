using System;
using System.Transactions;
using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    public class AddJoinToPipeCommandHandler : RequestHandler<AddJoinToPipeCommand>
    {
        readonly IAmARepository<Pipe> pipeRepository;
        readonly IAmACommandProcessor commandProcessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="pipeRepository"></param>
        /// <param name="commandProcessor"></param>
        /// <param name="logger">The logger.</param>
        public AddJoinToPipeCommandHandler(IAmARepository<Pipe> pipeRepository, IAmACommandProcessor commandProcessor, ILog logger) : base(logger)
        {
            this.pipeRepository = pipeRepository;
            this.commandProcessor = commandProcessor;
        }

        #region Overrides of RequestHandler<AddJoinToPipeCommand>

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="addJoinToPipeCommand">The command.</param>
        /// <returns>TRequest.</returns>
        public override AddJoinToPipeCommand Handle(AddJoinToPipeCommand addJoinToPipeCommand)
        {
            Pipe pipe;
            using (var scope = new TransactionScope())
            {
                pipe = pipeRepository[new Identity(addJoinToPipeCommand.PipeIdentity)];

                if (pipe == null)
                {
                    throw new PipeDoesNotExistException(string.Format("Pipe {0} not found", addJoinToPipeCommand.PipeIdentity));
                }

                //this creates the same join as added to the feed - but is a different instance. It will compare equal by value
                var join = new Join(pipe, new Uri(addJoinToPipeCommand.FeedAddress), new Address(addJoinToPipeCommand.AddressPattern));

                pipe.AddJoin(join);

                scope.Complete();
            }

            commandProcessor.Send(new AddJoinToFeedCommand(pipe, addJoinToPipeCommand.FeedAddress, addJoinToPipeCommand.AddressPattern));
            return base.Handle(addJoinToPipeCommand);
        }

        #endregion
    }
}
