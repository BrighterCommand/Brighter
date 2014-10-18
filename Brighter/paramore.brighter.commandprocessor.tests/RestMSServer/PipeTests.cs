using Common.Logging;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Handlers;
using paramore.brighter.restms.core.Ports.Repositories;

namespace paramore.commandprocessor.tests.RestMSServer
{
    public class When_adding_a_pipe
    {
        static AddPipeCommand addPipeCommand;
        static AddPipeCommandHandler addPipeCommandHandler;
        static IAmARepository<Pipe> pipeRepository; 
        static IAmACommandProcessor commandProcessor;

        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            commandProcessor = A.Fake<IAmACommandProcessor>();
            pipeRepository = new InMemoryRepositoryPipeRepository(logger);

            addPipeCommand = new AddPipeCommand("Default", "Default", "My Pipe");

            addPipeCommandHandler = new AddPipeCommandHandler(pipeRepository, commandProcessor, logger);

        };

        Because of = () => addPipeCommandHandler.Handle(addPipeCommand);

        It should_add_the_pipe_into_the_pipe_repository = () => pipeRepository[new Identity(addPipeCommand.Id.ToString())].Title.Value.ShouldEqual(addPipeCommand.Title);
        It should_use_the_command_identifier_as_the_pipe_identifier = () => pipeRepository[new Identity(addPipeCommand.Id.ToString())].Id.Value.ShouldEqual(addPipeCommand.Id.ToString());
        It should_use_the_command_identifier_as_the_pipe_name = () => pipeRepository[new Identity(addPipeCommand.Id.ToString())].Name.Value.ShouldEqual(addPipeCommand.Id.ToString());
        It should_have_uri_for_the_new_pipe = () => pipeRepository[new Identity(addPipeCommand.Id.ToString())].Href.AbsoluteUri.ShouldEqual(string.Format("http://{0}/restms/pipe/{1}", Globals.HostName, addPipeCommand.Id.ToString()));
        It should_have_the_type_of_the_pipe = () => pipeRepository[new Identity(addPipeCommand.Id.ToString())].Type.ShouldEqual(PipeType.Default);
        It should_raise_an_event_to_add_the_pipe_to_the_domain  = () => A.CallTo(() => commandProcessor.Send(A<AddPipeToDomainCommand>.Ignored)).MustHaveHappened();
    }
}
