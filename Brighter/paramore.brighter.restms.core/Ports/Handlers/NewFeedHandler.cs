using System;
using System.ComponentModel;
using System.Transactions;
using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    public class NewFeedHandler : RequestHandler<NewFeedCommand>
    {
        readonly IAmARepository<Feed> feedRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="feedRepository"></param>
        public NewFeedHandler(ILog logger, IAmARepository<Feed> feedRepository) : base(logger)
        {
            this.feedRepository = feedRepository;
        }

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override NewFeedCommand Handle(NewFeedCommand command)
        {
            using (var scope = new TransactionScope())
            {
                var feed = new Feed(
                    name: new Name(command.Name),
                    feedType: (FeedType) Enum.Parse(typeof (FeedType), command.Type),
                    title: new Title(command.Title),
                    license: new Name(command.Licence)
                    );

                feedRepository.Add(feed);
                scope.Complete();
            }
            return base.Handle(command);
        }
    }
}
