using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
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
    public class When_retrieving_a_pipe
    {
        const string ADDRESS_PATTERN = "*";
        static Pipe pipe;
        static Join join;
        static Message message;
        static PipeRetriever pipeRetriever;
        static RestMSPipe restMSpipe;
        static IAmARepository<Pipe> pipeRepository;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            Globals.HostName = "host.com";

            var feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            pipe = new Pipe(
                new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                PipeType.Fifo,
                new Title("My Title"));

            
            join = new Join(
                pipe,
                new Uri("http://host.com/restms/feed/myfeed"),
                new Address(ADDRESS_PATTERN));
            pipe.AddJoin(join);

             message = new Message(
                ADDRESS_PATTERN,
                feed.Href.AbsoluteUri,
                new NameValueCollection(),
                Attachment.CreateAttachmentFromString("", MediaTypeNames.Text.Plain)
                );

            pipe.AddMessage(message);

            pipeRepository = new InMemoryPipeRepository(logger);
            pipeRepository.Add(pipe);
            pipeRetriever = new PipeRetriever(pipeRepository);
        };


        Because of = () => restMSpipe = pipeRetriever.Retrieve(pipe.Name);

        It should_have_the_pipe_name = () => restMSpipe.Name.ShouldEqual(pipe.Name.Value);
        It should_have_the_pipe_type = () => restMSpipe.Type.ShouldEqual(pipe.Type.ToString());
        It should_have_the_pipe_title = () => restMSpipe.Title.ShouldEqual(pipe.Title.Value);
        It should_have_the_pipe_href = () => restMSpipe.Href.ShouldEqual(pipe.Href.AbsoluteUri);
        It should_have_the_join_associated_with_the_pipe = () => restMSpipe.Joins.First().Name.ShouldEqual(join.Name.Value);
        It should_have_a_link_for_the_message_associated_with_the_pipe = () => restMSpipe.Messages.First().Href.ShouldEqual(new Uri(string.Format("http://{0}/restms/pipe/{1}/message/{2}", Globals.HostName, message.PipeName.Value, message.MessageId)).AbsoluteUri);
    }

    public class When_retrieving_a_pipe_that_does_not_exist
    {
        const string ADDRESS_PATTERN = "*";
        static Pipe pipe;
        static Join join;
        static Message message;
        static PipeRetriever pipeRetriever;
        static IAmARepository<Pipe> pipeRepository;
        static bool exceptionWasThrown;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            Globals.HostName = "host.com";
            exceptionWasThrown = false;

            var feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            pipe = new Pipe(
                new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                PipeType.Fifo,
                new Title("My Title"));

            
            join = new Join(
                pipe,
                new Uri("http://host.com/restms/feed/myfeed"),
                new Address(ADDRESS_PATTERN));
            pipe.AddJoin(join);

             message = new Message(
                ADDRESS_PATTERN,
                feed.Href.AbsoluteUri,
                new NameValueCollection(),
                Attachment.CreateAttachmentFromString("", MediaTypeNames.Text.Plain)
                );

            pipe.AddMessage(message);

            pipeRepository = new InMemoryPipeRepository(logger);
            pipeRetriever = new PipeRetriever(pipeRepository);
        };


        Because of = () => { try { pipeRetriever.Retrieve(pipe.Name); } catch (PipeDoesNotExistException) { exceptionWasThrown = true; } };

        It should_have_thrown_an_exception = () => exceptionWasThrown.ShouldBeTrue();

    }

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

        It should_send_a_message_to_add_a_default_join_to_the_default_feed = () => A.CallTo(() => commandProcessor.Send(A<AddJoinToPipeCommand>.Ignored)).MustHaveHappened();
    }


    [Subject("Pipes, a user-defined feed")]
    public class When_deleting_a_pipe
    {
        const string ADDRESS_PATTERN = "*";
        static Pipe pipe;
        static Join join;
        static Message message;
        static IAmARepository<Pipe> pipeRepository;
        static DeletePipeCommand deletePipeCommand;
        static DeletePipeCommandHandler deletePipeCommandHandler;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            Globals.HostName = "host.com";

            var feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            pipe = new Pipe(
                new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                PipeType.Fifo,
                new Title("My Title"));

            
            join = new Join(
                pipe,
                new Uri("http://host.com/restms/feed/myfeed"),
                new Address(ADDRESS_PATTERN));
            pipe.AddJoin(join);

             message = new Message(
                ADDRESS_PATTERN,
                feed.Href.AbsoluteUri,
                new NameValueCollection(),
                Attachment.CreateAttachmentFromString("", MediaTypeNames.Text.Plain)
                );

            pipe.AddMessage(message);

            pipeRepository = new InMemoryPipeRepository(logger);
            pipeRepository.Add(pipe);

            deletePipeCommand = new DeletePipeCommand(pipe.Name.Value);
            deletePipeCommandHandler = new DeletePipeCommandHandler(pipeRepository, logger);
        };

        Because of = () => deletePipeCommandHandler.Handle(deletePipeCommand);

        It should_remove_the_pipe_from_the_repository = () => pipeRepository[pipe.Id].ShouldBeNull();

    }
}
