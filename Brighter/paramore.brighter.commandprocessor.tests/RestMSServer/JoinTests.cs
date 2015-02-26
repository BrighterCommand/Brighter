#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Linq;
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

namespace paramore.commandprocessor.tests.RestMSServer
{
    [Subject("A join, connection between a feed and a pipe")]
    public class When_retrieving_a_join
    {
        private const string ADDRESS_PATTERN = "Address Pattern";
        private const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        private static Join s_join;
        private static Pipe s_pipe;
        private static Feed s_feed;
        private static IAmARepository<Join> s_joinRepository;
        private static JoinRetriever s_joinRetriever;
        private static RestMSJoin s_restMSJoin;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();

            s_feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);

            s_join = new Join(s_pipe, s_feed.Href, new Address(ADDRESS_PATTERN));

            s_joinRepository = new InMemoryJoinRepository(logger);
            s_joinRepository.Add(s_join);

            s_joinRetriever = new JoinRetriever(s_joinRepository);
        };

        private Because _of = () => s_restMSJoin = s_joinRetriever.Retrieve(s_join.Name);

        private It _should_have_the_join_type = () => s_restMSJoin.Type.ShouldEqual(s_join.Type.ToString());
        private It _should_have_the_join_address = () => s_restMSJoin.Address.ShouldEqual(s_join.Address.Value);
        private It _should_have_the_join_feed = () => s_restMSJoin.Feed.ShouldEqual(s_join.FeedHref.AbsoluteUri);
    }

    [Subject("A join, connection between a feed and a pipe")]
    public class When_a_join_is_missing
    {
        private const string ADDRESS_PATTERN = "Address Pattern";
        private const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        private static Join s_join;
        private static Pipe s_pipe;
        private static Feed s_feed;
        private static IAmARepository<Join> s_joinRepository;
        private static JoinRetriever s_joinRetriever;
        private static bool s_exceptionWasThrown;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_exceptionWasThrown = false;

            s_feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);

            s_join = new Join(s_pipe, s_feed.Href, new Address(ADDRESS_PATTERN));

            s_joinRepository = new InMemoryJoinRepository(logger);

            s_joinRetriever = new JoinRetriever(s_joinRepository);
        };

        private Because _of = () => { try { s_joinRetriever.Retrieve(s_join.Name); } catch (JoinDoesNotExistException) { s_exceptionWasThrown = true; } };

        private It _should_throw_an_exception = () => s_exceptionWasThrown.ShouldBeTrue();
    };

    [Subject("A join, connection between a feed and a pipe")]
    public class When_adding_a_join_to_a_pipe
    {
        private const string ADDRESS_PATTERN = "Address Pattern";
        private const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        private static AddJoinToPipeCommand s_addJoinToPipeCommand;
        private static AddJoinToPipeCommandHandler s_addJoinToFeedCommandHandler;
        private static Feed s_feed;
        private static Pipe s_pipe;
        private static IAmARepository<Pipe> s_pipeRepository;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_commandProcessor = A.Fake<IAmACommandProcessor>();
            s_pipeRepository = new InMemoryPipeRepository(logger);

            s_feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);

            s_pipeRepository.Add(s_pipe);

            //in the POST to the Pipe we know: Pipe identity, the Uri of the feed, and the address type of the join
            //we don't know the identity of the feed
            s_addJoinToPipeCommand = new AddJoinToPipeCommand(s_pipe.Id.Value, s_feed.Href.AbsoluteUri, ADDRESS_PATTERN);

            s_addJoinToFeedCommandHandler = new AddJoinToPipeCommandHandler(s_pipeRepository, s_commandProcessor, logger);
        };

        private Because _of = () => s_addJoinToFeedCommandHandler.Handle(s_addJoinToPipeCommand);

        private It _should_add_the_join_to_the_pipe = () => s_pipe.Joins.First().Address.ShouldEqual(new Address(ADDRESS_PATTERN));
        private It _should_set_the_join_feed_uri = () => s_pipe.Joins.First().FeedHref.ShouldEqual(s_feed.Href);
        private It _should_have_the_default_join_type = () => s_pipe.Joins.First().Type.ShouldEqual(JoinType.Default);
        private It _should_have_a_reference_to_the_pipe = () => s_pipe.Joins.First().Pipe.ShouldEqual(s_pipe);
        private It _should_raise_an_event_to_add_the_join_to_the_feed = () => A.CallTo(() => s_commandProcessor.Send(A<AddJoinToFeedCommand>.Ignored)).MustHaveHappened();
    }

    [Subject("A join, connection between a feed and a pipe")]
    public class When_adding_a_join_to_a_pipe_and_the_pipe_does_not_exist
    {
        private const string ADDRESS_PATTERN = "Address Pattern";
        private const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        private static AddJoinToPipeCommand s_addJoinToPipeCommand;
        private static AddJoinToPipeCommandHandler s_addJoinToFeedCommandHandler;
        private static Feed s_feed;
        private static Pipe s_pipe;
        private static IAmARepository<Pipe> s_pipeRepository;
        private static IAmACommandProcessor s_commandProcessor;
        private static bool s_exceptionThrown = false;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_exceptionThrown = false;
            s_commandProcessor = A.Fake<IAmACommandProcessor>();
            s_pipeRepository = new InMemoryPipeRepository(logger);

            s_feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);

            s_addJoinToPipeCommand = new AddJoinToPipeCommand(s_pipe.Id.Value, s_feed.Href.AbsoluteUri, ADDRESS_PATTERN);

            s_addJoinToFeedCommandHandler = new AddJoinToPipeCommandHandler(s_pipeRepository, s_commandProcessor, logger);
        };

        private Because _of = () => { try { s_addJoinToFeedCommandHandler.Handle(s_addJoinToPipeCommand); } catch (PipeDoesNotExistException) { s_exceptionThrown = true; } };

        private It _should_throw_a_pipe_not_found_exception = () => s_exceptionThrown.ShouldBeTrue();
    }
    [Subject("A join, connection between a feed and a pipe")]
    public class When_adding_a_join_to_a_feed
    {
        private const string ADDRESS_PATTERN = "Address Pattern";
        private const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        private static AddJoinToFeedCommand s_addJoinToFeedCommand;
        private static AddJoinToFeedCommandHandler s_addJoinToFeedCommandHandler;
        private static Feed s_feed;
        private static Pipe s_pipe;
        private static IAmARepository<Feed> s_feedRepository;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_commandProcessor = A.Fake<IAmACommandProcessor>();
            s_feedRepository = new InMemoryFeedRepository(logger);

            s_feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);

            s_feedRepository.Add(s_feed);

            //in the POST to the Pipe we know: Pipe identity, the Uri of the feed, and the address type of the join
            //we don't know the identity of the feed
            s_addJoinToFeedCommand = new AddJoinToFeedCommand(s_pipe, s_feed.Href.AbsoluteUri, ADDRESS_PATTERN);

            s_addJoinToFeedCommandHandler = new AddJoinToFeedCommandHandler(s_feedRepository, s_commandProcessor, logger);
        };

        private Because _of = () => s_addJoinToFeedCommandHandler.Handle(s_addJoinToFeedCommand);

        private It _should_add_the_join_to_the_feed = () => s_feed.Joins[new Address(ADDRESS_PATTERN)].First().Address.ShouldEqual(new Address(ADDRESS_PATTERN));
        private It _should_set_the_join_feed_uri = () => s_feed.Joins[new Address(ADDRESS_PATTERN)].First().FeedHref.ShouldEqual(s_feed.Href);
        private It _should_have_the_default_join_type = () => s_feed.Joins[new Address(ADDRESS_PATTERN)].First().Type.ShouldEqual(JoinType.Default);
        private It _should_have_a_reference_to_the_pipe = () => s_feed.Joins[new Address(ADDRESS_PATTERN)].First().Pipe.ShouldEqual(s_pipe);
        private It _should_raise_an_event_to_add_the_join_to_the_repo = () => A.CallTo(() => s_commandProcessor.Send(A<AddJoinCommand>.Ignored)).MustHaveHappened();
    }

    [Subject("A join, connection between a feed and a pipe")]
    public class When_a_feed_does_not_exist
    {
        private const string ADDRESS_PATTERN = "Address Pattern";
        private const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        private static AddJoinToFeedCommand s_addJoinToFeedCommand;
        private static AddJoinToFeedCommandHandler s_addJoinToFeedCommandHandler;
        private static IAmARepository<Feed> s_feedRepository;
        private static Pipe s_pipe;
        private static bool s_exceptionThrown;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_commandProcessor = A.Fake<IAmACommandProcessor>();
            s_feedRepository = new InMemoryFeedRepository(logger);
            s_exceptionThrown = false;

            s_pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);

            s_addJoinToFeedCommand = new AddJoinToFeedCommand(s_pipe, "http://host.com/restms/feed/123", ADDRESS_PATTERN);

            s_addJoinToFeedCommandHandler = new AddJoinToFeedCommandHandler(s_feedRepository, s_commandProcessor, logger);
        };

        private Because _of = () => { try { s_addJoinToFeedCommandHandler.Handle(s_addJoinToFeedCommand); } catch (FeedDoesNotExistException) { s_exceptionThrown = true; } };

        private It _should_throw_a_feed_not_found_exception = () => s_exceptionThrown.ShouldBeTrue();
    }


    public class When_adding_a_join
    {
        private const string ADDRESS_PATTERN = "Address Pattern";
        private const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        private static AddJoinCommand s_addJoinCommand;
        private static AddJoinCommandHandler s_addJoinCommandHandler;
        private static IAmARepository<Join> s_joinRepository;
        private static Join s_join;
        private static Pipe s_pipe;
        private static Feed s_feed;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();

            s_feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);
            s_join = new Join(s_pipe, s_feed.Href, new Address(ADDRESS_PATTERN));

            s_addJoinCommand = new AddJoinCommand(s_join);

            s_joinRepository = new InMemoryJoinRepository(logger);

            s_addJoinCommandHandler = new AddJoinCommandHandler(s_joinRepository, logger);
        };

        private Because _of = () => s_addJoinCommandHandler.Handle(s_addJoinCommand);

        private It _should_add_the_join_to_the_repo = () => s_joinRepository[s_join.Id].ShouldNotBeNull();
    }
}
