// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        private const string ADDRESS_PATTERN = "*";
        private static Pipe s_pipe;
        private static Join s_join;
        private static Message s_message;
        private static PipeRetriever s_pipeRetriever;
        private static RestMSPipe s_restMSpipe;
        private static IAmARepository<Pipe> s_pipeRepository;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            Globals.HostName = "host.com";

            var feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(
                new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                PipeType.Fifo,
                new Title("My Title"));


            s_join = new Join(
                s_pipe,
                new Uri("http://host.com/restms/feed/myfeed"),
                new Address(ADDRESS_PATTERN));
            s_pipe.AddJoin(s_join);

            s_message = new Message(
               ADDRESS_PATTERN,
               feed.Href.AbsoluteUri,
               new NameValueCollection(),
               Attachment.CreateAttachmentFromString("", MediaTypeNames.Text.Plain)
               );

            s_pipe.AddMessage(s_message);

            s_pipeRepository = new InMemoryPipeRepository(logger);
            s_pipeRepository.Add(s_pipe);
            s_pipeRetriever = new PipeRetriever(s_pipeRepository);
        };


        private Because _of = () => s_restMSpipe = s_pipeRetriever.Retrieve(s_pipe.Name);

        private It _should_have_the_pipe_name = () => s_restMSpipe.Name.ShouldEqual(s_pipe.Name.Value);
        private It _should_have_the_pipe_type = () => s_restMSpipe.Type.ShouldEqual(s_pipe.Type.ToString());
        private It _should_have_the_pipe_title = () => s_restMSpipe.Title.ShouldEqual(s_pipe.Title.Value);
        private It _should_have_the_pipe_href = () => s_restMSpipe.Href.ShouldEqual(s_pipe.Href.AbsoluteUri);
        private It _should_have_the_join_associated_with_the_pipe = () => s_restMSpipe.Joins.First().Name.ShouldEqual(s_join.Name.Value);
        private It _should_have_a_link_for_the_message_associated_with_the_pipe = () => s_restMSpipe.Messages.First().Href.ShouldEqual(new Uri(string.Format("http://{0}/restms/pipe/{1}/message/{2}", Globals.HostName, s_message.PipeName.Value, s_message.MessageId)).AbsoluteUri);
    }

    public class When_retrieving_a_pipe_that_does_not_exist
    {
        private const string ADDRESS_PATTERN = "*";
        private static Pipe s_pipe;
        private static Join s_join;
        private static Message s_message;
        private static PipeRetriever s_pipeRetriever;
        private static IAmARepository<Pipe> s_pipeRepository;
        private static bool s_exceptionWasThrown;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            Globals.HostName = "host.com";
            s_exceptionWasThrown = false;

            var feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(
                new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                PipeType.Fifo,
                new Title("My Title"));


            s_join = new Join(
                s_pipe,
                new Uri("http://host.com/restms/feed/myfeed"),
                new Address(ADDRESS_PATTERN));
            s_pipe.AddJoin(s_join);

            s_message = new Message(
               ADDRESS_PATTERN,
               feed.Href.AbsoluteUri,
               new NameValueCollection(),
               Attachment.CreateAttachmentFromString("", MediaTypeNames.Text.Plain)
               );

            s_pipe.AddMessage(s_message);

            s_pipeRepository = new InMemoryPipeRepository(logger);
            s_pipeRetriever = new PipeRetriever(s_pipeRepository);
        };


        private Because _of = () => { try { s_pipeRetriever.Retrieve(s_pipe.Name); } catch (PipeDoesNotExistException) { s_exceptionWasThrown = true; } };

        private It _should_have_thrown_an_exception = () => s_exceptionWasThrown.ShouldBeTrue();
    }

    [Subject("Pipes, a user-defined feed")]
    public class When_adding_a_pipe
    {
        private static AddPipeCommand s_addPipeCommand;
        private static AddPipeCommandHandler s_addPipeCommandHandler;
        private static IAmARepository<Pipe> s_pipeRepository;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_commandProcessor = A.Fake<IAmACommandProcessor>();
            s_pipeRepository = new InMemoryPipeRepository(logger);

            s_addPipeCommand = new AddPipeCommand("Default", "Fifo ", "My Pipe");

            s_addPipeCommandHandler = new AddPipeCommandHandler(s_pipeRepository, s_commandProcessor, logger);
        };

        private Because _of = () => s_addPipeCommandHandler.Handle(s_addPipeCommand);

        private It _should_add_the_pipe_into_the_pipe_repository = () => s_pipeRepository[new Identity(s_addPipeCommand.Id.ToString())].Title.Value.ShouldEqual(s_addPipeCommand.Title);
        private It _should_use_the_command_identifier_as_the_pipe_identifier = () => s_pipeRepository[new Identity(s_addPipeCommand.Id.ToString())].Id.Value.ShouldEqual(s_addPipeCommand.Id.ToString());
        private It _should_use_the_command_identifier_as_the_pipe_name = () => s_pipeRepository[new Identity(s_addPipeCommand.Id.ToString())].Name.Value.ShouldEqual(s_addPipeCommand.Id.ToString());
        private It _should_have_uri_for_the_new_pipe = () => s_pipeRepository[new Identity(s_addPipeCommand.Id.ToString())].Href.AbsoluteUri.ShouldEqual(string.Format("http://{0}/restms/pipe/{1}", Globals.HostName, s_addPipeCommand.Id.ToString()));
        private It _should_have_the_type_of_the_pipe = () => s_pipeRepository[new Identity(s_addPipeCommand.Id.ToString())].Type.ShouldEqual(PipeType.Default);
        private It _should_raise_an_event_to_add_the_pipe_to_the_domain = () => A.CallTo(() => s_commandProcessor.Send(A<AddPipeToDomainCommand>.Ignored)).MustHaveHappened();
    }

    [Subject("Pipes, a user-defined feed")]
    public class When_adding_a_default_pipe
    {
        private static AddPipeCommand s_addPipeCommand;
        private static AddPipeCommandHandler s_addPipeCommandHandler;
        private static IAmARepository<Pipe> s_pipeRepository;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_commandProcessor = A.Fake<IAmACommandProcessor>();
            s_pipeRepository = new InMemoryPipeRepository(logger);

            s_addPipeCommand = new AddPipeCommand("Default", "Fifo ", "My Pipe");

            s_addPipeCommandHandler = new AddPipeCommandHandler(s_pipeRepository, s_commandProcessor, logger);
        };

        private Because _of = () => s_addPipeCommandHandler.Handle(s_addPipeCommand);

        private It _should_send_a_message_to_add_a_default_join_to_the_default_feed = () => A.CallTo(() => s_commandProcessor.Send(A<AddJoinToPipeCommand>.Ignored)).MustHaveHappened();
    }


    [Subject("Pipes, a user-defined feed")]
    public class When_deleting_a_pipe
    {
        private const string ADDRESS_PATTERN = "*";
        private static Pipe s_pipe;
        private static Join s_join;
        private static Message s_message;
        private static IAmARepository<Pipe> s_pipeRepository;
        private static DeletePipeCommand s_deletePipeCommand;
        private static DeletePipeCommandHandler s_deletePipeCommandHandler;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            Globals.HostName = "host.com";

            var feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(
                new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                PipeType.Fifo,
                new Title("My Title"));


            s_join = new Join(
                s_pipe,
                new Uri("http://host.com/restms/feed/myfeed"),
                new Address(ADDRESS_PATTERN));
            s_pipe.AddJoin(s_join);

            s_message = new Message(
               ADDRESS_PATTERN,
               feed.Href.AbsoluteUri,
               new NameValueCollection(),
               Attachment.CreateAttachmentFromString("", MediaTypeNames.Text.Plain)
               );

            s_pipe.AddMessage(s_message);

            s_pipeRepository = new InMemoryPipeRepository(logger);
            s_pipeRepository.Add(s_pipe);

            s_deletePipeCommand = new DeletePipeCommand(s_pipe.Name.Value);
            s_deletePipeCommandHandler = new DeletePipeCommandHandler(s_pipeRepository, logger);
        };

        private Because _of = () => s_deletePipeCommandHandler.Handle(s_deletePipeCommand);

        private It _should_remove_the_pipe_from_the_repository = () => s_pipeRepository[s_pipe.Id].ShouldBeNull();
    }
}
