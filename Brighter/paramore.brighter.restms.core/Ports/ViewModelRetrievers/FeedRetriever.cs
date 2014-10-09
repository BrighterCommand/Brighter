using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;

namespace paramore.brighter.restms.core.Ports.ViewModelRetrievers
{
    public class FeedRetriever
    {
        readonly IAmARepository<Feed> feedRepository;

        public FeedRetriever(IAmARepository<Feed> feedRepository)
        {
            this.feedRepository = feedRepository;
        }

        public RestMSFeed Retrieve(Name name)
        {
            var feed = feedRepository[new Identity(name.Value)];

            if (feed == null)
            {
                throw new FeedDoesNotExistException();
            }

            return new RestMSFeed(feed);
        }
    }
}
