// ***********************************************************************
// Assembly         : paramore.brighter.restms.server
// Author           : ian
// Created          : 09-27-2014
//
// Last Modified By : ian
// Last Modified On : 10-03-2014
// ***********************************************************************
// <copyright file="RestMSServerBuilder.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
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
using paramore.brighter.restms.server.Adapters.Security;
using Thinktecture.IdentityModel.Hawk.Core;
using Thinktecture.IdentityModel.Hawk.Core.Helpers;

namespace paramore.brighter.restms.server.Adapters.Service
{
    /// <summary>
    /// Sets up a default domain, which has a default feed implementing the Housecat pattern
    /// http://www.restms.org/wiki:housecat
    /// Housecat is a one-to-one messaging pattern in which a sender addresses a receiver by name. 
    /// The <a href="http://restms-v2.wdfiles.com/local--files/wiki%3Ahousecat/housecat.png">diagram</a> shows this pattern, 
    /// where Master refers to the sender, and Cat refers to the receiver. 
    /// The Router refers to a set of feeds and pipes, or other resources capable of queuing and routing messages.
    //  In the general decoupled messaging model, the cat reads from a private queue which subscribes to the named address, and the master 
    //  publishes messages to this named address. In a coupled model, the cat reads from a named queue and the master publishes into this queue directly.
    //  The RestMS 3/Defaults profile implements both coupled Housecat (using the default feed and default join) and decoupled Housecat 
    //  (using a dynamic feed and arbitrary joins).
    /// </summary>
    public class RestMSServerBuilder : IConfigureRestMSServers, IBuildARestMSService, IUseRepositories, IUseCredentials
    {
        IAmARepository<Domain> domainRepository;
        IAmARepository<Feed> feedRepository;

        /// <summary>
        /// Withes this instance.
        /// </summary>
        /// <returns>IUseRepositories.</returns>
        public IUseRepositories With()
        {
            return this;
        }

        /// <summary>
        /// Does this instance.
        /// </summary>
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


        /// <summary>
        /// Repositorieses the specified domain repository.
        /// </summary>
        /// <param name="domainRepository">The domain repository.</param>
        /// <param name="feedRepository">The feed repository.</param>
        /// <returns>IBuildARestMSService.</returns>
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
                Id = "dh37fgj492je",
                Algorithm = SupportedAlgorithms.SHA256,
                User = "Guest",
                Key = Convert.FromBase64String("wBgvhp1lZTr4Tb6K6+5OQa1bL9fxK7j8wBsepjqVNiQ=")
            });
        
            return this;
        }

    }

    /// <summary>
    /// Interface IConfigureRestMSServers{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    public interface IConfigureRestMSServers
    {
        /// <summary>
        /// Withes this instance.
        /// </summary>
        /// <returns>IUseRepositories.</returns>
        IUseRepositories With();
    }

    /// <summary>
    /// Interface IUseRepositories{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    public interface IUseRepositories
    {
        /// <summary>
        /// Repositorieses the specified domain repository.
        /// </summary>
        /// <param name="domainRepository">The domain repository.</param>
        /// <param name="feedRepository">The feed repository.</param>
        /// <returns>IBuildARestMSService.</returns>
        IUseCredentials Repositories(IAmARepository<Domain> domainRepository, IAmARepository<Feed> feedRepository);
    }

    /// <summary>
    /// Interface IUseCredentials
    /// </summary>
    public interface IUseCredentials
    {
        /// <summary>
        /// Securities the specified credentials storage.
        /// </summary>
        /// <param name="credentialsStorage">The credentials storage.</param>
        /// <returns>IBuildARestMSService.</returns>
        IBuildARestMSService Security(IAmACredentialStore credentialsStorage);
        
    }

    /// <summary>
    /// Interface IBuildARestMSService{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    public interface IBuildARestMSService
    {
        /// <summary>
        /// Does this instance.
        /// </summary>
        void Do();

    }
}
