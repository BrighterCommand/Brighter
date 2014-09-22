using System;
using paramore.brighter.restms.server.Ports.Common;

namespace paramore.brighter.restms.server.Model
{
    public class Feed : Resource, IAmAnAggregate
    {
        public Feed(Name name, Uri href, FeedType feedType = FeedType.Direct, Title title = null, Name license = null)
        {
            Href = href;
            Type = feedType;
            Name = name;
            Title = title;
            License = license;
        }

        public FeedType Type { get; private set; }
        public Title Title { get; private set; }
        public Name License { get; private set; }
        public Identity Id { get; private set; }
        public AggregateVersion Version { get; private set; }

    }
}
