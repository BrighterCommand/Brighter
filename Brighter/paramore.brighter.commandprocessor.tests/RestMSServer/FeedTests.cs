using System;
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
    public class When_a_producer_adds_a_new_feed
    {
        const string FEED_NAME = "testFeed";
        const string FEED_TYPE = "Direct";
        const string FEED_TITLE = "Test Feed";
        const string FEED_LICENSE = "License";
        const string DOMAIN_NAME = "Default";
        static NewFeedHandler newFeedHandler;
        static NewFeedCommand newFeedCommand;
        static IAmARepository<Feed> feedRepository;
        static IAmACommandProcessor commandProcessor;

        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            commandProcessor = A.Fake<IAmACommandProcessor>();
            feedRepository = new InMemoryFeedRepository(logger);

            newFeedHandler = new NewFeedHandler(logger, feedRepository, commandProcessor);

            newFeedCommand = new NewFeedCommand(domainName: DOMAIN_NAME, name: FEED_NAME, type: FEED_TYPE, title: FEED_TITLE, license: FEED_LICENSE);
        };

        Because of = () => newFeedHandler.Handle(newFeedCommand);

        It should_update_the_repository_with_the_new_feed = () => feedRepository[new Identity(FEED_NAME)].ShouldNotBeNull();
        It should_name_the_new_feed = () => feedRepository[new Identity(FEED_NAME)].Name.Value.ShouldEqual(FEED_NAME);
        It should_have_uri_for_the_new_feed = () => feedRepository[new Identity(FEED_NAME)].Href.AbsoluteUri.ShouldEqual(string.Format("http://{0}/restms/feed/{1}", Globals.HostName, FEED_NAME));
        It should_have_a_type_for_the_new_feed = () => feedRepository[new Identity(FEED_NAME)].Type.ShouldEqual(FeedType.Direct);
        It should_have_a_license_for_the_feed = () => feedRepository[new Identity(FEED_NAME)].License.Value.ShouldEqual(FEED_LICENSE);
        It should_raise_an_event_to_add_the_feed_to_the_domain = () => A.CallTo(() => commandProcessor.Send(A<AddFeedToDomainCommand>.Ignored)).MustHaveHappened();
    }

    public class When_a_producer_adds_a_new_feed_but_it_already_exists
    {
        const string DOMAIN_NAME = "default";
        const string FEED_NAME = "Default";
        static Domain domain;
        static Feed feed;
        static NewFeedHandler newFeedHandler;
        static NewFeedCommand newFeedCommand;
        static IAmACommandProcessor commandProcessor;
        static bool exceptionThrown = false;

        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            commandProcessor = A.Fake<IAmACommandProcessor>();
            exceptionThrown = false;

            domain = new Domain(
                name: new Name(DOMAIN_NAME),
                title: new Title("title"),
                profile: new Profile(
                    name: new Name(@"3/Defaults"),
                    href: new Uri(@"href://www.restms.org/spec:3/Defaults")
                    ),
                version: new AggregateVersion(0)
                );


            feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name(FEED_NAME),
                title: new Title("Default feed")
                );

            var feedRepository = new InMemoryFeedRepository(logger);
            feedRepository.Add(feed);

            domain.AddFeed(feed.Id);

            var domainRepository = new InMemoryDomainRepository(logger);
            domainRepository.Add(domain);

            newFeedHandler = new NewFeedHandler(logger, feedRepository, commandProcessor);

            newFeedCommand = new NewFeedCommand(domainName: DOMAIN_NAME, name: FEED_NAME, type: "Direct", title: "Default feed", license: "");
        };

        Because of = () => { try { newFeedHandler.Handle(newFeedCommand); } catch (FeedAlreadyExistsException fe) { exceptionThrown = true; }  };


        It should_throw_an_already_exists_exception = () => exceptionThrown.ShouldBeTrue();
    }
}
