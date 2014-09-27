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
using System.Transactions;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.server.Adapters.Service
{
    public class RestMSServerBuilder : IConfigureRestMSServers, IBuildARestMSService, IUseRepositories
    {
        IAmARepository<Domain> domainRepository;
        IAmARepository<Feed> feedRepository;

        public IUseRepositories With()
        {
            return this;
        }

        public void Do()
        {
            var domain = new Domain(
                name: new Name("default"),
                title: new Title("title"),
                profile: new Profile(
                    name: new Name(@"3/Defaults"),
                    href: new Uri(@"href://www.restms.org/spec:3/Defaults")
                    ),
                version: new AggregateVersion(0)
                );


            var feed = new Feed(
                feedType: FeedType.Direct,
                name: new Name("default"),
                title: new Title("Default feed"),
                href: new Uri(@"http://host.com/restms/feed/default")
                );

            using (var scope = new TransactionScope())
            {
                domainRepository.Add(domain);
                feedRepository.Add(feed);
                scope.Complete();
            }
        }


        public IBuildARestMSService Repositories(IAmARepository<Domain> domainRepository, IAmARepository<Feed> feedRepository)
        {
            this.domainRepository = domainRepository;
            this.feedRepository = feedRepository;
            return this;
        }
    }

    public interface IConfigureRestMSServers
    {
        IUseRepositories With();
    }

    public interface IUseRepositories
    {
        IBuildARestMSService Repositories(IAmARepository<Domain> domainRepository, IAmARepository<Feed> feedRepository);
    }
    public interface IBuildARestMSService
    {
        void Do();
    }
}
