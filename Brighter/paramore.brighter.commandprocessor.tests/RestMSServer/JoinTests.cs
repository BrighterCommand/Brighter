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
using Common.Logging;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.restms.core;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Handlers;
using paramore.brighter.restms.core.Ports.Repositories;

namespace paramore.commandprocessor.tests.RestMSServer
{
    [Subject("A join, connection between a feed and a pipe")]
    public class When_adding_a_join_to_a_pipe
    {
        const string ADDRESS_PATTERN = "Address Pattern";
        const string PIPE_NAME = "{E00B52BE-F2C4-4D7F-8472-403E5CC5AB4B}";
        static AddJoinToFeedCommand addJoinToFeedCommand;
        static AddJoinToFeedCommandHandler addJoinToFeedCommandHandler;
        static Feed feed;
        static Pipe pipe;
        static IAmARepository<Feed> feedRepository;
            
        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
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
            addJoinToFeedCommand = new AddJoinToFeedCommand(pipe.Id.Value, feed.Href.AbsoluteUri, ADDRESS_PATTERN);

            addJoinToFeedCommandHandler = new AddJoinToFeedCommandHandler(feedRepository, logger);
        };

        Because of = () => addJoinToFeedCommandHandler.Handle(addJoinToFeedCommand);

        It should_add_the_join_to_the_feed = () => feed.Joins[new Address(ADDRESS_PATTERN)].First().Address.ShouldEqual(new Address(ADDRESS_PATTERN));
        It should_set_the_join_feed_uri = () => feed.Joins[new Address(ADDRESS_PATTERN)].First().FeedHref.ShouldEqual(feed.Href);
        It should_have_the_default_join_type = () => feed.Joins[new Address(ADDRESS_PATTERN)].First().Type.ShouldEqual(JoinType.Default);
        It should_have_a_reference_to_the_pipe = () => feed.Joins[new Address(ADDRESS_PATTERN)].First().Pipe.ShouldEqual(pipe.Id);
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
        static bool exceptionThrown = false;
            
        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            feedRepository = new InMemoryFeedRepository(logger);
            exceptionThrown = false;

            pipe = new Pipe(new Identity(PIPE_NAME), PipeType.Default);

            addJoinToFeedCommand = new AddJoinToFeedCommand(pipe.Id.ToString(), "http://host.com/restms/feed/123", ADDRESS_PATTERN);

            addJoinToFeedCommandHandler = new AddJoinToFeedCommandHandler(feedRepository, logger);
        };

        Because of = () => { try { addJoinToFeedCommandHandler.Handle(addJoinToFeedCommand); } catch (FeedDoesNotExistException) { exceptionThrown = true; }};

        It should_throw_a_feed_not_found_exception = () => exceptionThrown.ShouldBeTrue();
    }
}
