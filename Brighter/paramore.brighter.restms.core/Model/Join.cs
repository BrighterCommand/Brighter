using System;

namespace paramore.brighter.restms.core.Model
{
    /*
     *    Join - a relationship between a pipe and a feed.
          Joins follow these rules:

        Joins specify criteria by which feeds route messages into pipes.
        Joins are always dynamic and always private.
        Clients MAY create joins at runtime, after creating pipes.
        To create a new join the client POSTs a join specification to the parent pipe URI.
        If either the feed or the pipe for a join is deleted, the join is also deleted.

     */
    public class Join
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public Join(Address address, Uri feedHref)
        {
            Address = address;
            FeedHref = feedHref;
            Type = JoinType.Default;
        }

        public Address Address { get; private set; }
        public Uri FeedHref { get; private set; }
        public JoinType Type { get; private set; }
    }
}
