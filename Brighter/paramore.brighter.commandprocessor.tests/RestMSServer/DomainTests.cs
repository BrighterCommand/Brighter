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

using System;
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
using paramore.brighter.restms.core.Ports.Resources;
using paramore.brighter.restms.core.Ports.ViewModelRetrievers;

namespace paramore.commandprocessor.tests.RestMSServer
{
    [Subject("Retrieving a domain via the view model")]
    public class When_retreiving_a_domain
    {
        private static DomainRetriever domainRetriever;
        private static RestMS defaultDomain;
        private static Domain domain;
        private static Feed feed;

        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();

            domain = new Domain(
                name: new Name("default"),
                title: new Title("title"),
                profile: new Profile(
                    name: new Name(@"3/Defaults"),
                    href: new Uri(@"href://www.restms.org/spec:3/Defaults")
                    ),
                version: new AggregateVersion(0)
                );


            feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            var feedRepository = new InMemoryFeedRepository(logger);
            feedRepository.Add(feed);

            domain.AddFeed(feed.Id);

            var domainRepository = new InMemoryDomainRepository(logger);
            domainRepository.Add(domain);

            domainRetriever = new DomainRetriever(feedRepository, domainRepository);
        };

        Because of = () => defaultDomain = domainRetriever.Retrieve(new Name("default"));

        It should_have_set_the_domain_name = () => defaultDomain.Name.ShouldEqual(domain.Name.Value);
        It should_have_set_the_title = () => defaultDomain.Title.ShouldEqual(domain.Title.Value);
        It should_have_set_the_profile_name = () => defaultDomain.Profile.Name.ShouldEqual(domain.Profile.Name.Value);
        It should_have_set_the_profile_href = () => defaultDomain.Profile.Href.ShouldEqual(domain.Profile.Href.AbsoluteUri);
        It should_have_set_the_feed_type = () => defaultDomain.Feeds[0].Type.ShouldEqual(feed.Type.ToString());
        It should_have_set_the_feed_name = () => defaultDomain.Feeds[0].Name.ShouldEqual(feed.Name.Value);
        It should_have_set_the_feed_title = () => defaultDomain.Feeds[0].Title.ShouldEqual(feed.Title.Value);
        It should_have_set_the_feed_address = () => defaultDomain.Feeds[0].Href.ShouldEqual(feed.Href.AbsoluteUri);
    }

    public class When_the_domain_is_not_found
    {
        const string DOMAIN_NAME = "Default";
        const string FEED_NAME = "Feed";
        static AddFeedToDomainCommandHandler addFeedToDomainCommandHandler;
        static AddFeedToDomainCommand addFeedToDomainCommand;
        static Domain domain;
        static bool exceptionThrown = false;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            exceptionThrown = false;
        
            var repository = new InMemoryDomainRepository(logger);

            addFeedToDomainCommandHandler = new AddFeedToDomainCommandHandler(repository, logger);
            addFeedToDomainCommand = new AddFeedToDomainCommand(domainName: DOMAIN_NAME, feedName: FEED_NAME);
        };

        Because of = () => { try { addFeedToDomainCommandHandler.Handle(addFeedToDomainCommand); } catch (DomainNotFoundException dfe) { exceptionThrown = true; } };

        It should_throw_an_exception_that_the_feed_already_exists = () => exceptionThrown.ShouldBeTrue();

    }

    [Subject("Updating the domain")]
    public class When_adding_a_feed_to_a_domain
    {
        const string DOMAIN_NAME = "Default";
        const string FEED_NAME = "Feeed";
        static AddFeedToDomainCommandHandler addFeedToDomainCommandHandler;
        static AddFeedToDomainCommand addFeedToDomainCommand;
        static Domain domain;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            domain = new Domain(
                name: new Name(DOMAIN_NAME), 
                title: new Title("Default domain"), 
                profile: new Profile(
                    name: new Name("3/Defaults"), 
                    href: new Uri("http://host.com/restms/feed/default")
                    )
                );

            var repository = new InMemoryDomainRepository(logger);
            repository.Add(domain);

            addFeedToDomainCommandHandler = new AddFeedToDomainCommandHandler(repository, logger);
            addFeedToDomainCommand = new AddFeedToDomainCommand(domainName: DOMAIN_NAME, feedName: FEED_NAME);
        };

        Because of = () => addFeedToDomainCommandHandler.Handle(addFeedToDomainCommand);

        It should_add_the_feed_to_the_domain = () => domain.Feeds.Any(feed => feed == new Identity(FEED_NAME)).ShouldBeTrue();
    }

    public class When_removing_a_feed_from_a_domain
    { 
        const string DOMAIN_NAME = "Default";
        const string FEED_NAME = "Feeed";
        static RemoveFeedFromDomainCommand removeFeedFromDomainCommand;
        static RemoveFeedFromDomainCommandHandler removeFeedFromDomainCommandHandler;
        static Domain domain;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            domain = new Domain(
                name: new Name(DOMAIN_NAME), 
                title: new Title("Default domain"), 
                profile: new Profile(
                    name: new Name("3/Defaults"), 
                    href: new Uri("http://host.com/restms/feed/default")
                    )
                );

            domain.AddFeed(new Identity(FEED_NAME));

            var repository = new InMemoryDomainRepository(logger);
            repository.Add(domain);

            removeFeedFromDomainCommand = new RemoveFeedFromDomainCommand(FEED_NAME);
            removeFeedFromDomainCommandHandler = new RemoveFeedFromDomainCommandHandler(repository, logger);
        };

        Because of = () => removeFeedFromDomainCommandHandler.Handle(removeFeedFromDomainCommand);

        It should_remove_the_feed_from_the_domain = () => domain.Feeds.Any(feed => feed == new Identity(FEED_NAME)).ShouldBeFalse();
    }

}
