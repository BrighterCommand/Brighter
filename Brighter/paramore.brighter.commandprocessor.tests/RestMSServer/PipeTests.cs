using System;
using System.Linq;
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
using paramore.brighter.restms.core.Ports.Resources;
using paramore.brighter.restms.core.Ports.ViewModelRetrievers;
using Message = paramore.brighter.restms.core.Model.Message;

namespace paramore.commandprocessor.tests.RestMSServer
{
    [Subject("Pipes, a user-defined feed")]
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
            pipeRepository = new InMemoryPipeRepository(logger);

            addPipeCommand = new AddPipeCommand("Default", "Fifo ", "My Pipe");

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

    [Subject("Pipes, a user-defined feed")]
    public class When_adding_a_default_pipe
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
            pipeRepository = new InMemoryPipeRepository(logger);

            addPipeCommand = new AddPipeCommand("Default", "Fifo ", "My Pipe");

            addPipeCommandHandler = new AddPipeCommandHandler(pipeRepository, commandProcessor, logger);

        };

        Because of = () => addPipeCommandHandler.Handle(addPipeCommand);

        It should_send_a_message_to_add_a_default_join_to_the_default_feed = () => A.CallTo(() => commandProcessor.Send(A<AddJoinToFeedCommand>.Ignored)).MustHaveHappened();
    }

    [Subject("Pipes, a user-defined feed")]
    public class When_retrieving_a_pipe
    {
        const string ADDRESS_PATTERN = "*";
        static Pipe pipe;
        static Join join;
        static Message message;
        static PipeRetriever pipeRetriever;
        static RestMSPipe restMSpipe;

        Establish context = () =>
                            {
                                pipe = new Pipe(
                                    new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                                    PipeType.Fifo,
                                    new Title("My Title"));

                                
                                join = new Join(
                                    pipe,
                                    new Uri("http://host.com/restms/feed/myfeed"),
                                    new Address(ADDRESS_PATTERN));
                                pipe.AddJoin(join);

                                /* TODO
                                 * message = new Message(
                                    ADDRESS_PATTERN,
                                    new Uri()
                                );*/

                            };


        Because of = () => restMSpipe = pipeRetriever.Retrieve(pipe.Name);

        It should_have_the_pipe_name = () => restMSpipe.Name.ShouldEqual(pipe.Name.Value);
        It should_have_the_pipe_type = () => restMSpipe.Type.ShouldEqual(pipe.Type.ToString());
        It should_have_the_pipe_title = () => restMSpipe.Title.ShouldEqual(pipe.Title.ToString());
        It should_have_the_pipe_href = () => restMSpipe.Href.ShouldEqual(pipe.Href.AbsoluteUri);
        It should_have_the_join_associated_with_the_pipe = () => restMSpipe.Joins.First().Name.ShouldEqual(join.Name.Value);
    }
}
