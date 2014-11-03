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
        /// <param name="joinRepository"></param>
        /// <param name="commandProcessor"></param>
        /// <param name="logger">The logger.</param>
        public AddJoinToPipeCommandHandler(IAmARepository<Pipe> pipeRepository, IAmACommandProcessor commandProcessor, ILog logger) : base(logger)
        {
            this.pipeRepository = pipeRepository;
            this.commandProcessor = commandProcessor;
        }

        #region Overrides of RequestHandler<AddJoinToPipeCommand>

        /// <summary>
        /// Handles the command to add a join.
        /// We follow a sequence: add to the pipe, add to the feed, add to the join repository. Each step is a transactional boundary, and we use events
        /// to pass control between the steps. The goal here is that failure is safe. If the add to pipe fails, the feed will not try to add to a pipe
        /// that is not aware of the join. If the add to feed fails, the pipe will just get no messages, and this should be discoverable by finding that the
        /// join does not exist. If the add to pipe and add to feed succeed, but the add to join repo fails you will get messages, but not be able to query the join
        /// directly.
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
