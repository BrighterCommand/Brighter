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
        const string ADDRESS_PATTERN = "Address Pattern";
        const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        static Join join;
        static Pipe pipe;
        static Feed feed;
        static IAmARepository<Join> joinRepository;
        static JoinRetriever joinRetriever;
        static RestMSJoin restMSJoin;

        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();

            feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);

            join = new Join(pipe, feed.Href, new Address(ADDRESS_PATTERN));

            joinRepository = new InMemoryJoinRepository(logger);
            joinRepository.Add(join);

            joinRetriever = new JoinRetriever(joinRepository);
        };

        Because of = () => restMSJoin = joinRetriever.Retrieve(join.Name);

        It should_have_the_join_type = () => restMSJoin.Type.ShouldEqual(join.Type.ToString());
        It should_have_the_join_address = () => restMSJoin.Address.ShouldEqual(join.Address.Value);
        It should_have_the_join_feed = () => restMSJoin.Feed.ShouldEqual(join.FeedHref.AbsoluteUri);

    }

    [Subject("A join, connection between a feed and a pipe")]
    public class When_a_join_is_missing
    {
        const string ADDRESS_PATTERN = "Address Pattern";
        const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        static Join join;
        static Pipe pipe;
        static Feed feed;
        static IAmARepository<Join> joinRepository;
        static JoinRetriever joinRetriever;
        static bool exceptionWasThrown;

        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            exceptionWasThrown = false;

            feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);

            join = new Join(pipe, feed.Href, new Address(ADDRESS_PATTERN));

            joinRepository = new InMemoryJoinRepository(logger);

            joinRetriever = new JoinRetriever(joinRepository);
        };

        Because of = () => { try { joinRetriever.Retrieve(join.Name); } catch (JoinDoesNotExistException) { exceptionWasThrown = true; } };

        It should_throw_an_exception = () => exceptionWasThrown.ShouldBeTrue();

    };

    [Subject("A join, connection between a feed and a pipe")]
    public class When_adding_a_join_to_a_pipe
    {
        const string ADDRESS_PATTERN = "Address Pattern";
        const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        static AddJoinToPipeCommand addJoinToPipeCommand;
        static AddJoinToPipeCommandHandler addJoinToFeedCommandHandler;
        static Feed feed;
        static Pipe pipe;
        static IAmARepository<Pipe> pipeRepository;
        static IAmACommandProcessor commandProcessor;
            
        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            commandProcessor = A.Fake<IAmACommandProcessor>();
            pipeRepository = new InMemoryPipeRepository(logger);

            feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);

            pipeRepository.Add(pipe);

            //in the POST to the Pipe we know: Pipe identity, the Uri of the feed, and the address type of the join
            //we don't know the identity of the feed
            addJoinToPipeCommand = new AddJoinToPipeCommand(pipe.Id.Value, feed.Href.AbsoluteUri, ADDRESS_PATTERN);

            addJoinToFeedCommandHandler = new AddJoinToPipeCommandHandler(pipeRepository, commandProcessor, logger);
        };

        Because of = () => addJoinToFeedCommandHandler.Handle(addJoinToPipeCommand);

        It should_add_the_join_to_the_pipe = () => pipe.Joins.First().Address.ShouldEqual(new Address(ADDRESS_PATTERN));
        It should_set_the_join_feed_uri = () => pipe.Joins.First().FeedHref.ShouldEqual(feed.Href);
        It should_have_the_default_join_type = () => pipe.Joins.First().Type.ShouldEqual(JoinType.Default);
        It should_have_a_reference_to_the_pipe = () => pipe.Joins.First().Pipe.ShouldEqual(pipe);
        It should_raise_an_event_to_add_the_join_to_the_feed  = () => A.CallTo(() => commandProcessor.Send(A<AddJoinToFeedCommand>.Ignored)).MustHaveHappened();
    }

    [Subject("A join, connection between a feed and a pipe")]
    public class When_adding_a_join_to_a_pipe_and_the_pipe_does_not_exist
    {
        const string ADDRESS_PATTERN = "Address Pattern";
        const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        static AddJoinToPipeCommand addJoinToPipeCommand;
        static AddJoinToPipeCommandHandler addJoinToFeedCommandHandler;
        static Feed feed;
        static Pipe pipe;
        static IAmARepository<Pipe> pipeRepository;
        static IAmACommandProcessor commandProcessor;
        static bool exceptionThrown = false;
        
        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            exceptionThrown = false;
            commandProcessor = A.Fake<IAmACommandProcessor>();
            pipeRepository = new InMemoryPipeRepository(logger);

            feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);

            addJoinToPipeCommand = new AddJoinToPipeCommand(pipe.Id.Value, feed.Href.AbsoluteUri, ADDRESS_PATTERN);

            addJoinToFeedCommandHandler = new AddJoinToPipeCommandHandler(pipeRepository, commandProcessor, logger);
        };

        Because of = () => {try { addJoinToFeedCommandHandler.Handle(addJoinToPipeCommand); } catch (PipeDoesNotExistException) { exceptionThrown = true; } };

        It should_throw_a_pipe_not_found_exception = () => exceptionThrown.ShouldBeTrue();
    }
    [Subject("A join, connection between a feed and a pipe")]
    public class When_adding_a_join_to_a_feed
    {
        const string ADDRESS_PATTERN = "Address Pattern";
        const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        static AddJoinToFeedCommand addJoinToFeedCommand;
        static AddJoinToFeedCommandHandler addJoinToFeedCommandHandler;
        static Feed feed;
        static Pipe pipe;
        static IAmARepository<Feed> feedRepository;
        static IAmACommandProcessor commandProcessor;
            
        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            commandProcessor = A.Fake<IAmACommandProcessor>();
            feedRepository = new InMemoryFeedRepository(logger);

            feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);

            feedRepository.Add(feed);

            //in the POST to the Pipe we know: Pipe identity, the Uri of the feed, and the address type of the join
            //we don't know the identity of the feed
            addJoinToFeedCommand = new AddJoinToFeedCommand(pipe, feed.Href.AbsoluteUri, ADDRESS_PATTERN);

            addJoinToFeedCommandHandler = new AddJoinToFeedCommandHandler(feedRepository, commandProcessor, logger);
        };

        Because of = () => addJoinToFeedCommandHandler.Handle(addJoinToFeedCommand);

        It should_add_the_join_to_the_feed = () => feed.Joins[new Address(ADDRESS_PATTERN)].First().Address.ShouldEqual(new Address(ADDRESS_PATTERN));
        It should_set_the_join_feed_uri = () => feed.Joins[new Address(ADDRESS_PATTERN)].First().FeedHref.ShouldEqual(feed.Href);
        It should_have_the_default_join_type = () => feed.Joins[new Address(ADDRESS_PATTERN)].First().Type.ShouldEqual(JoinType.Default);
        It should_have_a_reference_to_the_pipe = () => feed.Joins[new Address(ADDRESS_PATTERN)].First().Pipe.ShouldEqual(pipe);
        It should_raise_an_event_to_add_the_join_to_the_repo  = () => A.CallTo(() => commandProcessor.Send(A<AddJoinCommand>.Ignored)).MustHaveHappened();
    }

    [Subject("A join, connection between a feed and a pipe")]
    public class When_a_feed_does_not_exist
    {
        const string ADDRESS_PATTERN = "Address Pattern";
        const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        static AddJoinToFeedCommand addJoinToFeedCommand;
        static AddJoinToFeedCommandHandler addJoinToFeedCommandHandler;
        static IAmARepository<Feed> feedRepository;
        static Pipe pipe;
        static bool exceptionThrown;
        static IAmACommandProcessor commandProcessor;
            
        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            commandProcessor = A.Fake<IAmACommandProcessor>();
            feedRepository = new InMemoryFeedRepository(logger);
            exceptionThrown = false;

            pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);

            addJoinToFeedCommand = new AddJoinToFeedCommand(pipe, "http://host.com/restms/feed/123", ADDRESS_PATTERN);

            addJoinToFeedCommandHandler = new AddJoinToFeedCommandHandler(feedRepository, commandProcessor, logger);
        };

        Because of = () => { try { addJoinToFeedCommandHandler.Handle(addJoinToFeedCommand); } catch (FeedDoesNotExistException) { exceptionThrown = true; }};

        It should_throw_a_feed_not_found_exception = () => exceptionThrown.ShouldBeTrue();
    }


    public class When_adding_a_join
    {
        const string ADDRESS_PATTERN = "Address Pattern";
        const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        static AddJoinCommand addJoinCommand;
        static AddJoinCommandHandler addJoinCommandHandler;
        static IAmARepository<Join> joinRepository;
        static Join join;
        static Pipe pipe;
        static Feed feed;

        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();

            feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);
            join = new Join(pipe, feed.Href, new Address(ADDRESS_PATTERN));

            addJoinCommand = new AddJoinCommand(join);

            joinRepository = new InMemoryJoinRepository(logger);
            
            addJoinCommandHandler = new AddJoinCommandHandler(joinRepository, logger);

        };
            
        Because of = () => addJoinCommandHandler.Handle(addJoinCommand);

        It should_add_the_join_to_the_repo = () => joinRepository[join.Id].ShouldNotBeNull();
    }
}
