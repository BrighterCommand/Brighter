using System;
using System.Linq;
using System.Transactions;
using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    public class AddJoinToFeedCommandHandler : RequestHandler<AddJoinToFeedCommand>
    {
        readonly IAmARepository<Feed> feedRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public AddJoinToFeedCommandHandler(IAmARepository<Feed> feedRepository, ILog logger) : base(logger)
        {
            this.feedRepository = feedRepository;
        }

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="addJoinToFeedCommand">The command.</param>
        /// <returns>TRequest.</returns>
        public override AddJoinToFeedCommand Handle(AddJoinToFeedCommand addJoinToFeedCommand)
        {
            using (var scope = new TransactionScope())
            {
                var feedUri = new Uri(addJoinToFeedCommand.FeedAddress);
                var feed = feedRepository.Find(f => f.Href == feedUri).First();
                if (feed == null)
                {
                    throw new FeedDoesNotExistException();
                }

                var join = new Join(
                    new Address(addJoinToFeedCommand.AddressPattern),
                    new Uri(addJoinToFeedCommand.FeedAddress));

                feed.AddJoin(join);

                scope.Complete();
            }
            return base.Handle(addJoinToFeedCommand);
        }
    }
}
