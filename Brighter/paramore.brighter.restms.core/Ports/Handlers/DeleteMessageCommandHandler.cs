using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Ports.Commands;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    public class DeleteMessageCommandHandler : RequestHandler<DeleteMessageCommand>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public DeleteMessageCommandHandler(ILog logger) : base(logger)
        {
        }
    }
}
