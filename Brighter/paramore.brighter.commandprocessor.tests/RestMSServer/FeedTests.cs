using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
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

namespace paramore.commandprocessor.tests.RestMSServer
{
    [Subject("Retrieving a feed via the view model")]
    public class When_retreiving_a_feed
    {
        static FeedRetriever feedRetriever;
        static RestMSFeed restMSfeed;
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

            var feedRepository = new InMemoryFeedRepository(logger);
            feedRepository.Add(feed);


            feedRetriever = new FeedRetriever(feedRepository);
        };

        Because of = () => restMSfeed = feedRetriever.Retrieve(new Name("default"));

        It should_have_set_the_feed_type = () => restMSfeed.Type.ShouldEqual(feed.Type.ToString());
        It should_have_set_the_feed_name = () => restMSfeed.Name.ShouldEqual(feed.Name.Value);
        It should_have_set_the_feed_title = () => restMSfeed.Title.ShouldEqual(feed.Title.Value);
        It should_have_set_the_feed_address = () => restMSfeed.Href.ShouldEqual(feed.Href.AbsoluteUri);
    }


    [Subject("Retrieving a feed via the view model")]
    public class When_retrieving_a_missing_feed
    {
        static FeedRetriever feedRetriever;
        static bool caughtException;

        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();

            var feedRepository = new InMemoryFeedRepository(logger);

            feedRetriever = new FeedRetriever(feedRepository);
        };

       Because of = () => {try{feedRetriever.Retrieve(new Name("default"));}catch (FeedDoesNotExistException){caughtException = true;}};

       It should_raise_a_feed_not_found_exception = () => caughtException.ShouldBeTrue();
    }

    [Subject("Adding a feed")]
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

    [Subject("Adding a feed")]
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

        Because of = () => { try { newFeedHandler.Handle(newFeedCommand); } catch (FeedAlreadyExistsException) { exceptionThrown = true; }  };


        It should_throw_an_already_exists_exception = () => exceptionThrown.ShouldBeTrue();
    }

    [Subject("Deleting a feed")]
    public class When_deleting_a_feed
    {
        const string DOMAIN_NAME = "default";
        const string FEED_NAME = "MyFeed";
        static Domain domain;
        static Feed feed;
        static DeleteFeedCommand deleteFeedCommand;
        static DeleteFeedCommandHandler deleteFeedCommandHandler;
        static InMemoryFeedRepository feedRepository;
        static IAmACommandProcessor commandProcessor;

        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            commandProcessor = A.Fake<IAmACommandProcessor>();

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
                title: new Title("My feed")
                );

            feedRepository = new InMemoryFeedRepository(logger);
            feedRepository.Add(feed);

            domain.AddFeed(feed.Id);

            var domainRepository = new InMemoryDomainRepository(logger);
            domainRepository.Add(domain);
            
            deleteFeedCommandHandler = new DeleteFeedCommandHandler(feedRepository, commandProcessor, logger);
            deleteFeedCommand = new DeleteFeedCommand(FEED_NAME);

        };

        Because of = () => deleteFeedCommandHandler.Handle(deleteFeedCommand);

        It should_remove_the_feed_from_the_repository = () => feedRepository[new Identity(FEED_NAME)].ShouldBeNull();
        It should_send_a_message_to_remove_feed_from_domain = () => A.CallTo(() => commandProcessor.Send(A<RemoveFeedFromDomainCommand>.Ignored)).MustHaveHappened();

    }

    [Subject("Deleting a feed")]
    public class When_deleting_a_feed_that_does_not_exist
    {
        const string FEED_NAME = "MissingFeed";
        static DeleteFeedCommand deleteFeedCommand;
        static DeleteFeedCommandHandler deleteFeedCommandHandler;
        static InMemoryFeedRepository feedRepository;
        static IAmACommandProcessor commandProcessor;
        static bool exceptionThrown = false;

        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            commandProcessor = A.Fake<IAmACommandProcessor>();
            exceptionThrown = false;

            feedRepository = new InMemoryFeedRepository(logger);

            deleteFeedCommandHandler = new DeleteFeedCommandHandler(feedRepository, commandProcessor, logger);
            deleteFeedCommand = new DeleteFeedCommand(FEED_NAME);

        };

        Because of = () => { try { deleteFeedCommandHandler.Handle(deleteFeedCommand); } catch (FeedDoesNotExistException) { exceptionThrown = true; } };

        It should_throw_a_feed_not_found_exeption = () => exceptionThrown.ShouldBeTrue();

    }

    public class When_trying_to_delete_the_default_feed
    {
        const string FEED_NAME = "Default";
        const string DOMAIN_NAME = "default";
        static Domain domain;
        static Feed feed;
        static DeleteFeedCommand deleteFeedCommand;
        static DeleteFeedCommandHandler deleteFeedCommandHandler;
        static InMemoryFeedRepository feedRepository;
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
                feedType: FeedType.Default,
                name: new Name(FEED_NAME),
                title: new Title("Default feed")
                );

            feedRepository = new InMemoryFeedRepository(logger);
            feedRepository.Add(feed);

            domain.AddFeed(feed.Id);

            var domainRepository = new InMemoryDomainRepository(logger);
            domainRepository.Add(domain);

            deleteFeedCommandHandler = new DeleteFeedCommandHandler(feedRepository, commandProcessor, logger);
            deleteFeedCommand = new DeleteFeedCommand(FEED_NAME);

        };

        Because of = () => { try { deleteFeedCommandHandler.Handle(deleteFeedCommand); } catch (InvalidOperationException) { exceptionThrown = true; } };

        It should_throw_an_invalid_operation_exception = () => exceptionThrown.ShouldBeTrue();
        
    }

    public class When_posting_a_message_to_a_feed
    {
        static AddMessageToFeedCommand addMessageToFeedCommand;
        static AddMessageToFeedCommandHandler addmessageToFeedCommandHandler;
        const string DOMAIN_NAME = "default";
        const string FEED_NAME = "Default";
        const string REPLY_TO = "http://host.com/mywebhook/messagereceipt";
        const string MESSAGE_ADDRESS = "*";
        const string MY_CUSTOM_HEADER = "Hunting";
        const string MY_HEADER_VALUE = "Snark";
        const string MESSAGE_CONTENT = "I am some content";
        static Domain domain;
        static Feed feed;
        static Pipe pipe;
        static Join join;
        static NameValueCollection headers;
        static Attachment content;

        Establish context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();

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

            pipe = new Pipe(Guid.NewGuid().ToString(), "Default");

            join = new Join(pipe, feed.Href, new Address(MESSAGE_ADDRESS));
            pipe.AddJoin(join);
            feed.AddJoin(join);

            var feedRepository = new InMemoryFeedRepository(logger);
            feedRepository.Add(feed);

            domain.AddFeed(feed.Id);

            var domainRepository = new InMemoryDomainRepository(logger);
            domainRepository.Add(domain);

            var pipeRepository = new InMemoryPipeRepository(logger);
            pipeRepository.Add(pipe);


            headers = new NameValueCollection {{MY_CUSTOM_HEADER, MY_HEADER_VALUE}};
            content = Attachment.CreateAttachmentFromString(MESSAGE_CONTENT, MediaTypeNames.Text.Plain);
            addMessageToFeedCommand = new AddMessageToFeedCommand(feed.Name.Value, MESSAGE_ADDRESS, REPLY_TO, headers, content);
            addmessageToFeedCommandHandler = new AddMessageToFeedCommandHandler(feedRepository, logger);
            
        };

        Because of = () => addmessageToFeedCommandHandler.Handle(addMessageToFeedCommand);

        It should_distribute_the_message_to_every_subscribed_join = () => addMessageToFeedCommand.MatchingJoins.ShouldEqual(1);
        It should_put_the_message_onto_the_pipe_associated_with_the_join = () => pipe.Messages.Count().ShouldEqual(1);
        It should_store_the_routing_address_on_the_message = () => pipe.Messages.First().Address.Value.ShouldEqual(MESSAGE_ADDRESS);
        It should_store_a_message_id_for_the_message = () => pipe.Messages.First().MessageId.ShouldNotEqual(Guid.Empty);
        It should_store_a_reply_to_for_the_message = () => pipe.Messages.First().ReplyTo.AbsoluteUri.ShouldEqual(addMessageToFeedCommand.ReplyTo);
        It should_add_any_headers_to_the_message = () => pipe.Messages.First().Headers[MY_CUSTOM_HEADER].ShouldEqual(headers[MY_CUSTOM_HEADER]);
        It should_have_content_with_a_matching_content_type = () => pipe.Messages.First().Content.ContentType.MediaType.ShouldEqual(content.ContentType.MediaType);
        It should_have_content_with_a_matching_encoding = () => pipe.Messages.First().Content.Encoding.ShouldEqual(content.TransferEncoding);
        It should_have_matching_content = () => pipe.Messages.First().Content.AsString().ShouldEqual(MESSAGE_CONTENT);

    }

}
