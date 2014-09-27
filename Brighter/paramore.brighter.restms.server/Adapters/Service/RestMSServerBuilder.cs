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
