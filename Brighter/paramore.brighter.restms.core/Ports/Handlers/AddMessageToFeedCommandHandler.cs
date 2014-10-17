using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Ports.Commands;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    public class AddMessageToFeedCommandHandler : RequestHandler<AddMessageToFeedCommand>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public AddMessageToFeedCommandHandler(ILog logger) : base(logger)
        {
        }
    }
}
