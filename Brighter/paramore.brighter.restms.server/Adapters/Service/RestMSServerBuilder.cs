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
using paramore.brighter.restms.server.Adapters.Configuration;
using paramore.brighter.restms.server.Adapters.Security;
using Thinktecture.IdentityModel.Hawk.Core;
using Thinktecture.IdentityModel.Hawk.Core.Helpers;

namespace paramore.brighter.restms.server.Adapters.Service
{
    //  In the general decoupled messaging model, the cat reads from a private queue which subscribes to the named address, and the master 
    //  publishes messages to this named address. In a coupled model, the cat reads from a named queue and the master publishes into this queue directly.
    //  The RestMS 3/Defaults profile implements both coupled Housecat (using the default feed and default join) and decoupled Housecat 
    //  (using a dynamic feed and arbitrary joins).
    /// </summary>
    internal class RestMSServerBuilder : IConfigureRestMSServers, IBuildARestMSService, IUseRepositories, IUseCredentials
    {
        IAmARepository<Domain> domainRepository;
        IAmARepository<Feed> feedRepository;
        RestMSServerConfiguration configuration;

        public IUseRepositories With()
        {
            configuration = RestMSServerConfiguration.GetConfiguration();
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
                feedType: FeedType.Default,
                name: new Name("default"),
                title: new Title("Default feed")
                );

            domain.AddFeed(feed.Id);

            using (var scope = new TransactionScope())
            {
                domainRepository.Add(domain);
                feedRepository.Add(feed);
                scope.Complete();
            }
        }


        public IUseCredentials  Repositories(IAmARepository<Domain> domainRepository, IAmARepository<Feed> feedRepository)
        {
            this.domainRepository = domainRepository;
            this.feedRepository = feedRepository;
            return this;
        }

        /// <summary>
        /// Securities the specified credentials storage.
        /// </summary>
        /// <param name="credentialsStorage">The credentials storage.</param>
        /// <returns>IBuildARestMSService.</returns>
        public IBuildARestMSService Security(IAmACredentialStore credentialsStorage)
        {
            credentialsStorage.Add(new Credential()
            {
                Id = configuration.Admin.Id,
                Algorithm = SupportedAlgorithms.SHA256,
                User = configuration.Admin.User,
                Key = Convert.FromBase64String(configuration.Admin.Key)
            });
        
            return this;
        }

    }

    internal interface IConfigureRestMSServers
    {
        /// <summary>
        /// Withes this instance.
        /// </summary>
        /// <returns>IUseRepositories.</returns>
        IUseRepositories With();
    }

    internal interface IUseRepositories
    {
        /// <summary>
        /// Repositorieses the specified domain repository.
        /// </summary>
        /// <param name="domainRepository">The domain repository.</param>
        /// <param name="feedRepository">The feed repository.</param>
        /// <returns>IBuildARestMSService.</returns>
        IUseCredentials Repositories(IAmARepository<Domain> domainRepository, IAmARepository<Feed> feedRepository);
    }

    internal interface IUseCredentials
    {
        IBuildARestMSService Security(IAmACredentialStore credentialsStorage);
        
    }

    internal interface IBuildARestMSService
    {
        void Do();

    }
}
