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
    public class When_a_producer_adds_a_new_feed
    {
        const string FEED_NAME = "testFeed";
        const string FEED_TYPE = "Direct";
        const string FEED_TITLE = "Test Feed";
        const string FEED_LICENCE = "Licence";
        static NewFeedHandler newFeedHandler;
        static NewFeedCommand newFeedCommand;
        static IAmARepository<Feed> feedRepository; 
        
        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            feedRepository = new InMemoryFeedRepository(logger);
            Globals.HostName = "host.com";

            newFeedHandler = new NewFeedHandler(logger, feedRepository);

            newFeedCommand = new NewFeedCommand(name: FEED_NAME, type: FEED_TYPE, title: FEED_TITLE, licence: FEED_LICENCE);
        };

        Because of = () => newFeedHandler.Handle(newFeedCommand);

        It should_update_the_repository_with_the_new_feed = () => feedRepository[new Identity(FEED_NAME)].ShouldNotBeNull();
        It should_name_the_new_feed = () => feedRepository[new Identity(FEED_NAME)].Name.Value.ShouldEqual(FEED_NAME);
        It should_have_uri_for_the_new_feed = () => feedRepository[new Identity(FEED_NAME)].Href.AbsoluteUri.ShouldEqual(string.Format("http://{0}/restms/feed/{1}", Globals.HostName, FEED_NAME));
        It should_have_a_type_for_the_new_feed = () => feedRepository[new Identity(FEED_NAME)].Type.ShouldEqual(FeedType.Direct);
        It should_have_a_licence_for_the_feed = () => feedRepository[new Identity(FEED_NAME)].License.Value.ShouldEqual(FEED_LICENCE);
        It shoud_add_the_feed_to_the_domain = () => false.ShouldBeTrue();
    }

    public class When_a_producer_adds_a_new_feed_but_it_already_exists
    {
        It should_update_the_existing_feed = () => false.ShouldBeTrue();
    }
}
