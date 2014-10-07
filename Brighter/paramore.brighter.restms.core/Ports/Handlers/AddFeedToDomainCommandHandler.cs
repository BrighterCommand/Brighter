using System.Transactions;
using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Repositories;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    public class AddFeedToDomainCommandHandler : RequestHandler<AddFeedToDomainCommand>
    {
        readonly InMemoryDomainRepository repository;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="logger">The logger.</param>
        public AddFeedToDomainCommandHandler(InMemoryDomainRepository repository, ILog logger) : base(logger)
        {
            this.repository = repository;
        }

        /// <summary>
        /// Adds the feed to the specified domain
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override AddFeedToDomainCommand Handle(AddFeedToDomainCommand command)
        {
            using (var scope = new TransactionScope())
            {
                var domain = repository[new Identity(command.DomainName)];
                if (domain == null)
                {
                    throw new DomainNotFoundException();
                }

                domain.AddFeed(new Identity(command.FeedName));
                scope.Complete();
            }

            return base.Handle(command);
        }

    }
}