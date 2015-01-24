using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Ports.Cache;
using paramore.brighter.restms.core.Ports.Commands;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    public class CacheCleaningHandler: RequestHandler<InvalidateCacheCommand>
    {
        readonly IAmACache cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="cache">The cache we should clear; implementors will need to wrap their cache in this</param>
        /// <param name="logger">The logger.</param>
        public CacheCleaningHandler(IAmACache cache, ILog logger) : base(logger)
        {
            this.cache = cache;
        }

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override InvalidateCacheCommand Handle(InvalidateCacheCommand command)
        {
            cache.InvalidateResource(command.ResourceToInvalidate);
            return base.Handle(command);
        }

    }
}
