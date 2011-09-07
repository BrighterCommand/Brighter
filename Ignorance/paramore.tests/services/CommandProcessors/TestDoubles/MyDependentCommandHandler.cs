using Paramore.Infrastructure.Domain;
using Paramore.Services.CommandHandlers;

namespace Paramore.Tests.services.CommandProcessors.TestDoubles
{
    internal class MyDependentCommandHandler : RequestHandler<MyCommand>
    {
        private readonly IRepository<MyEntity, MyEntityDTO> repository;
        private static MyCommand command;

        public MyDependentCommandHandler(IRepository<MyEntity, MyEntityDTO> repository)
        {
            this.repository = repository;
            command = null;
        }

        public override MyCommand Handle(MyCommand command)
        {
            LogCommand(command);
            return base.Handle(command);
        }

        public static bool ShouldRecieve(MyCommand expectedCommand)
        {
            return (command != null) && (expectedCommand.Id == command.Id);
        }

        private void LogCommand(MyCommand request)
        {
            command = request;
        }
    }
}