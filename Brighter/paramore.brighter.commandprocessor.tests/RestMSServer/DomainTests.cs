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
using FakeItEasy;
using Machine.Specifications;
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
    [Subject("Retrieving a domain via the view model")]
    public class When_retreiving_a_domain
    {
        private static DomainRetriever s_domainRetriever;
        private static RestMSDomain s_defaultDomain;
        private static Domain s_domain;
        private static Feed s_feed;
        private static Pipe s_pipe;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();

            s_domain = new Domain(
                name: new Name("default"),
                title: new Title("title"),
                profile: new Profile(
                    name: new Name(@"3/Defaults"),
                    href: new Uri(@"href://www.restms.org/spec:3/Defaults")
                    ),
                version: new AggregateVersion(0)
                );


            s_feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            var feedRepository = new InMemoryFeedRepository(logger);
            feedRepository.Add(s_feed);

            s_domain.AddFeed(s_feed.Id);

            s_pipe = new Pipe(
                new Identity("{B5A49969-DCAA-4885-AFAE-A574DED1E96A}"),
                PipeType.Fifo,
                new Title("My Title"));

            var pipeRepository = new InMemoryPipeRepository(logger);
            pipeRepository.Add(s_pipe);

            s_domain.AddPipe(s_pipe.Id);

            var domainRepository = new InMemoryDomainRepository(logger);
            domainRepository.Add(s_domain);

            s_domainRetriever = new DomainRetriever(domainRepository, feedRepository, pipeRepository);
        };

        private Because _of = () => s_defaultDomain = s_domainRetriever.Retrieve(new Name("default"));

        private It _should_have_set_the_domain_name = () => s_defaultDomain.Name.ShouldEqual(s_domain.Name.Value);
        private It _should_have_set_the_title = () => s_defaultDomain.Title.ShouldEqual(s_domain.Title.Value);
        private It _should_have_set_the_profile_name = () => s_defaultDomain.Profile.Name.ShouldEqual(s_domain.Profile.Name.Value);
        private It _should_have_set_the_profile_href = () => s_defaultDomain.Profile.Href.ShouldEqual(s_domain.Profile.Href.AbsoluteUri);
        private It _should_have_set_the_domain_href = () => s_defaultDomain.Href.ShouldEqual(s_domain.Href.AbsoluteUri);
        private It _should_have_set_the_feed_type = () => s_defaultDomain.Feeds[0].Type.ShouldEqual(s_feed.Type.ToString());
        private It _should_have_set_the_feed_name = () => s_defaultDomain.Feeds[0].Name.ShouldEqual(s_feed.Name.Value);
        private It _should_have_set_the_feed_title = () => s_defaultDomain.Feeds[0].Title.ShouldEqual(s_feed.Title.Value);
        private It _should_have_set_the_feed_address = () => s_defaultDomain.Feeds[0].Href.ShouldEqual(s_feed.Href.AbsoluteUri);
        private It _should_have_set_the_pipe_name = () => s_defaultDomain.Pipes[0].Name.ShouldEqual(s_pipe.Name.Value);
        private It _should_have_set_the_pipe_title = () => s_defaultDomain.Pipes[0].Title.ShouldEqual(s_pipe.Title.Value);
        private It _should_have_set_the_pipe_type = () => s_defaultDomain.Pipes[0].Type.ShouldEqual(s_pipe.Type.ToString());
        private It _should_have_set_the_pipe_href = () => s_defaultDomain.Pipes[0].Href.ShouldEqual(s_pipe.Href.AbsoluteUri);
    }

    [Subject("Retrieving a domain via the view model")]
    public class When_adding_a_feed_and_the_domain_is_not_found
    {
        private const string DOMAIN_NAME = "Default";
        private const string FEED_NAME = "Feed";
        private static AddFeedToDomainCommandHandler s_addFeedToDomainCommandHandler;
        private static AddFeedToDomainCommand s_addFeedToDomainCommand;
        private static bool s_exceptionThrown = false;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_exceptionThrown = false;

            var repository = new InMemoryDomainRepository(logger);

            s_addFeedToDomainCommandHandler = new AddFeedToDomainCommandHandler(repository, logger);
            s_addFeedToDomainCommand = new AddFeedToDomainCommand(domainName: DOMAIN_NAME, feedName: FEED_NAME);
        };

        private Because _of = () => { try { s_addFeedToDomainCommandHandler.Handle(s_addFeedToDomainCommand); } catch (DomainDoesNotExistException) { s_exceptionThrown = true; } };

        private It _should_throw_an_exception_that_the_feed_already_exists = () => s_exceptionThrown.ShouldBeTrue();
    }

    [Subject("Updating the domain")]
    public class When_adding_a_feed_to_a_domain
    {
        private const string DOMAIN_NAME = "Default";
        private const string FEED_NAME = "Feeed";
        private static AddFeedToDomainCommandHandler s_addFeedToDomainCommandHandler;
        private static AddFeedToDomainCommand s_addFeedToDomainCommand;
        private static Domain s_domain;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_domain = new Domain(
                name: new Name(DOMAIN_NAME),
                title: new Title("Default domain"),
                profile: new Profile(
                    name: new Name("3/Defaults"),
                    href: new Uri("http://host.com/restms/feed/default")
                    )
                );

            var repository = new InMemoryDomainRepository(logger);
            repository.Add(s_domain);

            s_addFeedToDomainCommandHandler = new AddFeedToDomainCommandHandler(repository, logger);
            s_addFeedToDomainCommand = new AddFeedToDomainCommand(domainName: DOMAIN_NAME, feedName: FEED_NAME);
        };

        private Because _of = () => s_addFeedToDomainCommandHandler.Handle(s_addFeedToDomainCommand);

        private It _should_add_the_feed_to_the_domain = () => s_domain.Feeds.Any(feed => feed == new Identity(FEED_NAME)).ShouldBeTrue();
    }

    [Subject("Updating the domain")]
    public class When_removing_a_feed_from_a_domain
    {
        private const string DOMAIN_NAME = "Default";
        private const string FEED_NAME = "Feeed";
        private static RemoveFeedFromDomainCommand s_removeFeedFromDomainCommand;
        private static RemoveFeedFromDomainCommandHandler s_removeFeedFromDomainCommandHandler;
        private static Domain s_domain;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_domain = new Domain(
                name: new Name(DOMAIN_NAME),
                title: new Title("Default domain"),
                profile: new Profile(
                    name: new Name("3/Defaults"),
                    href: new Uri("http://host.com/restms/feed/default")
                    )
                );

            s_domain.AddFeed(new Identity(FEED_NAME));

            var repository = new InMemoryDomainRepository(logger);
            repository.Add(s_domain);

            s_removeFeedFromDomainCommand = new RemoveFeedFromDomainCommand(FEED_NAME);
            s_removeFeedFromDomainCommandHandler = new RemoveFeedFromDomainCommandHandler(repository, logger);
        };

        private Because _of = () => s_removeFeedFromDomainCommandHandler.Handle(s_removeFeedFromDomainCommand);

        private It _should_remove_the_feed_from_the_domain = () => s_domain.Feeds.Any(feed => feed == new Identity(FEED_NAME)).ShouldBeFalse();
    }

    [Subject("Updating the domain")]
    public class When_adding_a_pipe_to_a_domain
    {
        private const string DOMAIN_NAME = "Default";
        private const string PIPE_NAME = "{A9343B6D-ACA2-4D9E-ACFE-78998267C678}";
        private static Domain s_domain;
        private static AddPipeToDomainCommandHandler s_addPipeToDomainCommandHandler;
        private static AddPipeToDomainCommand s_addPipeToDomainCommand;

        private Establish _context = () =>
        {
            Globals.HostName = "host.com";
            var logger = A.Fake<ILog>();
            s_domain = new Domain(
                name: new Name(DOMAIN_NAME),
                title: new Title("Default domain"),
                profile: new Profile(
                    name: new Name("3/Defaults"),
                    href: new Uri("http://host.com/restms/feed/default")
                    )
                );

            var repository = new InMemoryDomainRepository(logger);
            repository.Add(s_domain);

            s_addPipeToDomainCommandHandler = new AddPipeToDomainCommandHandler(repository, logger);
            s_addPipeToDomainCommand = new AddPipeToDomainCommand(domainName: DOMAIN_NAME, pipeName: PIPE_NAME);
        };

        private Because _of = () => s_addPipeToDomainCommandHandler.Handle(s_addPipeToDomainCommand);

        private It _should_add_the_pipe_into_the_domain = () => s_domain.Pipes.Any(pipe => pipe == new Identity(PIPE_NAME)).ShouldBeTrue();
    }

    [Subject("Retrieving a domain via the view model")]
    public class When_adding_a_pipe_and_the_domain_is_not_found
    {
        private const string DOMAIN_NAME = "Default";
        private const string PIPE_NAME = "{A9343B6D-ACA2-4D9E-ACFE-78998267C678}";
        private static AddPipeToDomainCommandHandler s_addPipeToDomainCommandHandler;
        private static AddPipeToDomainCommand s_addPipeToDomainCommand;
        private static bool s_exceptionThrown = false;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_exceptionThrown = false;

            var repository = new InMemoryDomainRepository(logger);

            s_addPipeToDomainCommandHandler = new AddPipeToDomainCommandHandler(repository, logger);
            s_addPipeToDomainCommand = new AddPipeToDomainCommand(domainName: DOMAIN_NAME, pipeName: PIPE_NAME);
        };

        private Because _of = () => { try { s_addPipeToDomainCommandHandler.Handle(s_addPipeToDomainCommand); } catch (DomainDoesNotExistException) { s_exceptionThrown = true; } };

        private It _should_throw_an_exception_that_the_feed_already_exists = () => s_exceptionThrown.ShouldBeTrue();
    }
}
