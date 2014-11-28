using System;
using paramore.brighter.restms.core.Ports.Resources;

namespace paramore.brighter.restms.server.Adapters.Formatters
{
    public interface IParseDomainPosts
    {
        Tuple<ParseResult, RestMSFeed, RestMSPipeNew> Parse(string body);
    }
}