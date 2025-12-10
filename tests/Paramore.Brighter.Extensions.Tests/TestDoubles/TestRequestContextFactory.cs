using Paramore.Brighter;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles
{
    public class TestRequestContextFactory : IAmARequestContextFactory
    {
        public RequestContext Create()
        {
            return new RequestContext();
        }
    }
}
