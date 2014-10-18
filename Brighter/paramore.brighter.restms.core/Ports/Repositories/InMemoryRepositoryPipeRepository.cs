using Common.Logging;
using paramore.brighter.restms.core.Model;

namespace paramore.brighter.restms.core.Ports.Repositories
{
    public class InMemoryRepositoryPipeRepository : InMemoryRepository<Pipe>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public InMemoryRepositoryPipeRepository(ILog logger) : base(logger)
        {
        }
    }
}
