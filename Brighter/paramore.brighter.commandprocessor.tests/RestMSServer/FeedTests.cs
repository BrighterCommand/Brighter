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

namespace paramore.commandprocessor.tests.RestMSServer
{
    [Subject("Retrieving a feed via the view model")]
    public class When_retreiving_a_feed
    {
        private static FeedRetriever s_feedRetriever;
        private static RestMSFeed s_restMSfeed;
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

            var feedRepository = new InMemoryFeedRepository(logger);
            feedRepository.Add(s_feed);


            s_feedRetriever = new FeedRetriever(feedRepository);
        };

        private Because _of = () => s_restMSfeed = s_feedRetriever.Retrieve(new Name("default"));

        private It _should_have_set_the_feed_type = () => s_restMSfeed.Type.ShouldEqual(s_feed.Type.ToString());
        private It _should_have_set_the_feed_name = () => s_restMSfeed.Name.ShouldEqual(s_feed.Name.Value);
        private It _should_have_set_the_feed_title = () => s_restMSfeed.Title.ShouldEqual(s_feed.Title.Value);
        private It _should_have_set_the_feed_address = () => s_restMSfeed.Href.ShouldEqual(s_feed.Href.AbsoluteUri);
    }


    [Subject("Retrieving a feed via the view model")]
    public class When_retrieving_a_missing_feed
    {
        private static FeedRetriever s_feedRetriever;
        private static bool s_caughtException;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();

            var feedRepository = new InMemoryFeedRepository(logger);

            s_feedRetriever = new FeedRetriever(feedRepository);
        };

        private Because _of = () => { try { s_feedRetriever.Retrieve(new Name("default")); } catch (FeedDoesNotExistException) { s_caughtException = true; } };

        private It _should_raise_a_feed_not_found_exception = () => s_caughtException.ShouldBeTrue();
    }

    [Subject("Adding a feed")]
    public class When_a_producer_adds_a_new_feed
    {
        private const string FEED_NAME = "testFeed";
        private const string FEED_TYPE = "Direct";
        private const string FEED_TITLE = "Test Feed";
        private const string FEED_LICENSE = "License";
        private const string DOMAIN_NAME = "Default";
        private static AddFeedCommandHandler s_addFeedCommandHandler;
        private static AddFeedCommand s_addFeedCommand;
        private static IAmARepository<Feed> s_feedRepository;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_commandProcessor = A.Fake<IAmACommandProcessor>();
            s_feedRepository = new InMemoryFeedRepository(logger);

            s_addFeedCommandHandler = new AddFeedCommandHandler(logger, s_feedRepository, s_commandProcessor);

            s_addFeedCommand = new AddFeedCommand(domainName: DOMAIN_NAME, name: FEED_NAME, type: FEED_TYPE, title: FEED_TITLE, license: FEED_LICENSE);
        };

        private Because _of = () => s_addFeedCommandHandler.Handle(s_addFeedCommand);

        private It _should_update_the_repository_with_the_new_feed = () => s_feedRepository[new Identity(FEED_NAME)].ShouldNotBeNull();
        private It _should_name_the_new_feed = () => s_feedRepository[new Identity(FEED_NAME)].Name.Value.ShouldEqual(FEED_NAME);
        private It _should_have_uri_for_the_new_feed = () => s_feedRepository[new Identity(FEED_NAME)].Href.AbsoluteUri.ShouldEqual(string.Format("http://{0}/restms/feed/{1}", Globals.HostName, FEED_NAME));
        private It _should_have_a_type_for_the_new_feed = () => s_feedRepository[new Identity(FEED_NAME)].Type.ShouldEqual(FeedType.Direct);
        private It _should_have_a_license_for_the_feed = () => s_feedRepository[new Identity(FEED_NAME)].License.Value.ShouldEqual(FEED_LICENSE);
        private It _should_raise_an_event_to_add_the_feed_to_the_domain = () => A.CallTo(() => s_commandProcessor.Send(A<AddFeedToDomainCommand>.Ignored)).MustHaveHappened();
    }

    [Subject("Adding a feed")]
    public class When_a_producer_adds_a_new_feed_but_it_already_exists
    {
        private const string DOMAIN_NAME = "default";
        private const string FEED_NAME = "Default";
        private static Domain s_domain;
        private static Feed s_feed;
        private static AddFeedCommandHandler s_addFeedCommandHandler;
        private static AddFeedCommand s_addFeedCommand;
        private static IAmACommandProcessor s_commandProcessor;
        private static bool s_exceptionThrown = false;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_commandProcessor = A.Fake<IAmACommandProcessor>();
            s_exceptionThrown = false;

            s_domain = new Domain(
                name: new Name(DOMAIN_NAME),
                title: new Title("title"),
                profile: new Profile(
                    name: new Name(@"3/Defaults"),
                    href: new Uri(@"href://www.restms.org/spec:3/Defaults")
                    ),
                version: new AggregateVersion(0)
                );


            s_feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name(FEED_NAME),
                title: new Title("Default feed")
                );

            var feedRepository = new InMemoryFeedRepository(logger);
            feedRepository.Add(s_feed);

            s_domain.AddFeed(s_feed.Id);

            var domainRepository = new InMemoryDomainRepository(logger);
            domainRepository.Add(s_domain);

            s_addFeedCommandHandler = new AddFeedCommandHandler(logger, feedRepository, s_commandProcessor);

            s_addFeedCommand = new AddFeedCommand(domainName: DOMAIN_NAME, name: FEED_NAME, type: "Direct", title: "Default feed", license: "");
        };

        private Because _of = () => { try { s_addFeedCommandHandler.Handle(s_addFeedCommand); } catch (FeedAlreadyExistsException) { s_exceptionThrown = true; } };


        private It _should_throw_an_already_exists_exception = () => s_exceptionThrown.ShouldBeTrue();
    }

    [Subject("Deleting a feed")]
    public class When_deleting_a_feed
    {
        private const string DOMAIN_NAME = "default";
        private const string FEED_NAME = "MyFeed";
        private static Domain s_domain;
        private static Feed s_feed;
        private static DeleteFeedCommand s_deleteFeedCommand;
        private static DeleteFeedCommandHandler s_deleteFeedCommandHandler;
        private static InMemoryFeedRepository s_feedRepository;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_commandProcessor = A.Fake<IAmACommandProcessor>();

            s_domain = new Domain(
                name: new Name(DOMAIN_NAME),
                title: new Title("title"),
                profile: new Profile(
                    name: new Name(@"3/Defaults"),
                    href: new Uri(@"href://www.restms.org/spec:3/Defaults")
                    ),
                version: new AggregateVersion(0)
                );


            s_feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name(FEED_NAME),
                title: new Title("My feed")
                );

            s_feedRepository = new InMemoryFeedRepository(logger);
            s_feedRepository.Add(s_feed);

            s_domain.AddFeed(s_feed.Id);

            var domainRepository = new InMemoryDomainRepository(logger);
            domainRepository.Add(s_domain);

            s_deleteFeedCommandHandler = new DeleteFeedCommandHandler(s_feedRepository, s_commandProcessor, logger);
            s_deleteFeedCommand = new DeleteFeedCommand(FEED_NAME);
        };

        private Because _of = () => s_deleteFeedCommandHandler.Handle(s_deleteFeedCommand);

        private It _should_remove_the_feed_from_the_repository = () => s_feedRepository[new Identity(FEED_NAME)].ShouldBeNull();
        private It _should_send_a_message_to_remove_feed_from_domain = () => A.CallTo(() => s_commandProcessor.Send(A<RemoveFeedFromDomainCommand>.Ignored)).MustHaveHappened();
    }

    [Subject("Deleting a feed")]
    public class When_deleting_a_feed_that_does_not_exist
    {
        private const string FEED_NAME = "MissingFeed";
        private static DeleteFeedCommand s_deleteFeedCommand;
        private static DeleteFeedCommandHandler s_deleteFeedCommandHandler;
        private static InMemoryFeedRepository s_feedRepository;
        private static IAmACommandProcessor s_commandProcessor;
        private static bool s_exceptionThrown = false;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_commandProcessor = A.Fake<IAmACommandProcessor>();
            s_exceptionThrown = false;

            s_feedRepository = new InMemoryFeedRepository(logger);

            s_deleteFeedCommandHandler = new DeleteFeedCommandHandler(s_feedRepository, s_commandProcessor, logger);
            s_deleteFeedCommand = new DeleteFeedCommand(FEED_NAME);
        };

        private Because _of = () => { try { s_deleteFeedCommandHandler.Handle(s_deleteFeedCommand); } catch (FeedDoesNotExistException) { s_exceptionThrown = true; } };

        private It _should_throw_a_feed_not_found_exeption = () => s_exceptionThrown.ShouldBeTrue();
    }

    public class When_trying_to_delete_the_default_feed
    {
        private const string FEED_NAME = "Default";
        private const string DOMAIN_NAME = "default";
        private static Domain s_domain;
        private static Feed s_feed;
        private static DeleteFeedCommand s_deleteFeedCommand;
        private static DeleteFeedCommandHandler s_deleteFeedCommandHandler;
        private static InMemoryFeedRepository s_feedRepository;
        private static IAmACommandProcessor s_commandProcessor;
        private static bool s_exceptionThrown = false;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_commandProcessor = A.Fake<IAmACommandProcessor>();
            s_exceptionThrown = false;

            s_domain = new Domain(
                name: new Name(DOMAIN_NAME),
                title: new Title("title"),
                profile: new Profile(
                    name: new Name(@"3/Defaults"),
                    href: new Uri(@"href://www.restms.org/spec:3/Defaults")
                    ),
                version: new AggregateVersion(0)
                );


            s_feed = new Feed(
                feedType: FeedType.Default,
                name: new Name(FEED_NAME),
                title: new Title("Default feed")
                );

            s_feedRepository = new InMemoryFeedRepository(logger);
            s_feedRepository.Add(s_feed);

            s_domain.AddFeed(s_feed.Id);

            var domainRepository = new InMemoryDomainRepository(logger);
            domainRepository.Add(s_domain);

            s_deleteFeedCommandHandler = new DeleteFeedCommandHandler(s_feedRepository, s_commandProcessor, logger);
            s_deleteFeedCommand = new DeleteFeedCommand(FEED_NAME);
        };

        private Because _of = () => { try { s_deleteFeedCommandHandler.Handle(s_deleteFeedCommand); } catch (InvalidOperationException) { s_exceptionThrown = true; } };

        private It _should_throw_an_invalid_operation_exception = () => s_exceptionThrown.ShouldBeTrue();
    }

    public class When_posting_a_message_to_a_feed
    {
        private static AddMessageToFeedCommand s_addMessageToFeedCommand;
        private static AddMessageToFeedCommandHandler s_addmessageToFeedCommandHandler;
        private static IAmACommandProcessor s_commandProcessor;
        private const string DOMAIN_NAME = "default";
        private const string FEED_NAME = "Default";
        private const string REPLY_TO = "http://host.com/mywebhook/messagereceipt";
        private const string MESSAGE_ADDRESS = "*";
        private const string MY_CUSTOM_HEADER = "Hunting";
        private const string MY_HEADER_VALUE = "Snark";
        private const string MESSAGE_CONTENT = "I am some content";
        private static Domain s_domain;
        private static Feed s_feed;
        private static Pipe s_pipe;
        private static Join s_join;
        private static NameValueCollection s_headers;
        private static Attachment s_content;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_commandProcessor = A.Fake<IAmACommandProcessor>();

            s_domain = new Domain(
                name: new Name(DOMAIN_NAME),
                title: new Title("title"),
                profile: new Profile(
                    name: new Name(@"3/Defaults"),
                    href: new Uri(@"href://www.restms.org/spec:3/Defaults")
                    ),
                version: new AggregateVersion(0)
                );

            s_feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name(FEED_NAME),
                title: new Title("Default feed")
                );

            s_pipe = new Pipe(Guid.NewGuid().ToString(), "Default");

            s_join = new Join(s_pipe, s_feed.Href, new Address(MESSAGE_ADDRESS));
            s_pipe.AddJoin(s_join);
            s_feed.AddJoin(s_join);

            var feedRepository = new InMemoryFeedRepository(logger);
            feedRepository.Add(s_feed);

            s_domain.AddFeed(s_feed.Id);

            var domainRepository = new InMemoryDomainRepository(logger);
            domainRepository.Add(s_domain);

            var pipeRepository = new InMemoryPipeRepository(logger);
            pipeRepository.Add(s_pipe);


            s_headers = new NameValueCollection { { MY_CUSTOM_HEADER, MY_HEADER_VALUE } };
            s_content = Attachment.CreateAttachmentFromString(MESSAGE_CONTENT, MediaTypeNames.Text.Plain);
            s_addMessageToFeedCommand = new AddMessageToFeedCommand(s_feed.Name.Value, MESSAGE_ADDRESS, REPLY_TO, s_headers, s_content);
            s_addmessageToFeedCommandHandler = new AddMessageToFeedCommandHandler(feedRepository, s_commandProcessor, logger);
        };

        private Because _of = () => s_addmessageToFeedCommandHandler.Handle(s_addMessageToFeedCommand);

        private It _should_distribute_the_message_to_every_subscribed_join = () => s_addMessageToFeedCommand.MatchingJoins.ShouldEqual(1);
        private It _should_put_the_message_onto_the_pipe_associated_with_the_join = () => s_pipe.Messages.Count().ShouldEqual(1);
        private It _should_store_the_routing_address_on_the_message = () => s_pipe.Messages.First().Address.Value.ShouldEqual(MESSAGE_ADDRESS);
        private It _should_store_a_message_id_for_the_message = () => s_pipe.Messages.First().MessageId.ShouldNotEqual(Guid.Empty);
        private It _should_store_a_reply_to_for_the_message = () => s_pipe.Messages.First().ReplyTo.AbsoluteUri.ShouldEqual(s_addMessageToFeedCommand.ReplyTo);
        private It _should_add_any_headers_to_the_message = () => s_pipe.Messages.First().Headers[MY_CUSTOM_HEADER].ShouldEqual(s_headers[MY_CUSTOM_HEADER]);
        private It _should_have_content_with_a_matching_content_type = () => s_pipe.Messages.First().Content.ContentType.MediaType.ShouldEqual(s_content.ContentType.MediaType);
        private It _should_have_content_with_a_matching_encoding = () => s_pipe.Messages.First().Content.Encoding.ShouldEqual(s_content.TransferEncoding);
        private It _should_have_matching_content = () => s_pipe.Messages.First().Content.AsString().ShouldEqual(MESSAGE_CONTENT);
        private It _should_publish_cache_invalidation_to_all_changed_pipes = () => A.CallTo(() => s_commandProcessor.Send(A<InvalidateCacheCommand>.Ignored)).MustHaveHappened();
    }
}
