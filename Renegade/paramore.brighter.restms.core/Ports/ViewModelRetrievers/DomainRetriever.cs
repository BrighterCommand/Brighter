// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 09-26-2014
//
// Last Modified By : ian
// Last Modified On : 10-14-2014
// ***********************************************************************
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

using System.Collections.Generic;
using paramore.brighter.restms.core.Extensions;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;

namespace paramore.brighter.restms.core.Ports.ViewModelRetrievers
{
    /// <summary>
    /// </summary>
    public class DomainRetriever
    {
        private readonly IAmARepository<Feed> _feedRepository;
        private readonly IAmARepository<Pipe> _pipeRepository;
        private readonly IAmARepository<Domain> _domainRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainRetriever"/> class.
        /// </summary>
        /// <param name="domainRepository">The domain repository.</param>
        /// <param name="feedRepository">The feed repository.</param>
        public DomainRetriever(IAmARepository<Domain> domainRepository, IAmARepository<Feed> feedRepository, IAmARepository<Pipe> pipeRepository)
        {
            _feedRepository = feedRepository;
            _pipeRepository = pipeRepository;
            _domainRepository = domainRepository;
        }

        /// <summary>
        /// Retrieves the specified name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>RestMSDomain.</returns>
        /// <exception cref="DomainDoesNotExistException"></exception>
        public RestMSDomain Retrieve(Name name)
        {
            var domain = _domainRepository[new Identity(name.Value)];

            if (domain == null)
            {
                throw new DomainDoesNotExistException(string.Format("Could not find domain {0}", name.Value));
            }

            var feeds = new List<Feed>();
            domain.Feeds.Each(feed => feeds.Add(_feedRepository[new Identity(feed.Value)]));

            var pipes = new List<Pipe>();
            domain.Pipes.Each(pipe => pipes.Add(_pipeRepository[new Identity(pipe.Value)]));

            return new RestMSDomain(domain, feeds, pipes);
        }
    }
}
