using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    public class DeletePipeCommandHandler : RequestHandler<DeletePipeCommand>
    {
        readonly IAmARepository<Pipe> pipeRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="pipeRepository"></param>
        /// <param name="logger">The logger.</param>
        public DeletePipeCommandHandler(IAmARepository<Pipe> pipeRepository, ILog logger) : base(logger)
        {
            this.pipeRepository = pipeRepository;
        }

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="deletePipeCommand">The command.</param>
        /// <returns>TRequest.</returns>
        public override DeletePipeCommand Handle(DeletePipeCommand deletePipeCommand)
        {
            var pipe = pipeRepository[new Identity(deletePipeCommand.PipeName)];
            if (pipe == null)
            {
                throw new PipeDoesNotExistException();
            }

            pipeRepository.Remove(pipe.Id);

            return base.Handle(deletePipeCommand);
        }

    }
}
