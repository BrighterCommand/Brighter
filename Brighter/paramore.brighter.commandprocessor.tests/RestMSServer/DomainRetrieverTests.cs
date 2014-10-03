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
using Machine.Specifications;
using paramore.brighter.restms.core;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Repositories;
using paramore.brighter.restms.core.Ports.Resources;
using paramore.brighter.restms.core.Ports.ViewModelRetrievers;

namespace paramore.commandprocessor.tests.RestMSServer
{
    [Subject("Retrieving a domain via the view model")]
    public class When_retreiving_a_domain
    {
        private static DomainRetriever domainRetriever;
        private static RestMSDomain defaultDomain;
        private static Domain domain;
        private static Feed feed;

        Establish context = () =>
        {
            Globals.HostName = "host.com";

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

            var feedRepository = new InMemoryFeedRepository();
            feedRepository.Add(feed);

            domain.AddFeed(feed.Id);

            var domainRepository = new InMemoryDomainRepository();
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
}
