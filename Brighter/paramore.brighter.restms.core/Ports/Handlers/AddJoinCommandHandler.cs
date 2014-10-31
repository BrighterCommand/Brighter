using System.Transactions;
using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    public class AddJoinCommandHandler : RequestHandler<AddJoinCommand>
    {
        readonly IAmARepository<Join> joinRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="joinRepository"></param>
        /// <param name="logger">The logger.</param>
        public AddJoinCommandHandler(IAmARepository<Join> joinRepository, ILog logger) : base(logger)
        {
            this.joinRepository = joinRepository;
        }


        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="addJoinCommand">The command.</param>
        /// <returns>TRequest.</returns>
        public override AddJoinCommand Handle(AddJoinCommand addJoinCommand)
        {
            using (var scope = new TransactionScope())
            {
                joinRepository.Add(addJoinCommand.Join);

                scope.Complete();
            }

            return base.Handle(addJoinCommand);
        }

    }
}
