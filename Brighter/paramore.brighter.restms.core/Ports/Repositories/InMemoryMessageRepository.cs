using Common.Logging;
using paramore.brighter.restms.core.Model;

namespace paramore.brighter.restms.core.Ports.Repositories
{
    public class InMemoryMessageRepository : InMemoryRepository<Message>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public InMemoryMessageRepository(ILog logger) : base(logger)
        {
        }
    }
}
