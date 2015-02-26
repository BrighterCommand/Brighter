// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ***********************************************************************
// Assembly         : paramore.brighter.restms.server
// Author           : ian
// Created          : 11-05-2014
//
// Last Modified By : ian
// Last Modified On : 11-06-2014
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
using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;
using paramore.brighter.restms.core.Ports.ViewModelRetrievers;
using paramore.brighter.restms.server.Adapters.Filters;
using paramore.brighter.restms.server.Adapters.Formatters;

namespace paramore.brighter.restms.server.Adapters.Controllers
{
    /// <summary>
    /// Class DomainController.
    /// </summary>
    [Authorize]
    public class DomainController : ApiController
    {
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly IAmARepository<Domain> _domainRepository;
        private readonly IAmARepository<Feed> _feedRepository;
        private readonly IAmARepository<Pipe> _pipeRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainController"/> class.
        /// </summary>
        /// <param name="commandProcessor">The command processor.</param>
        /// <param name="domainRepository">The domain repository.</param>
        /// <param name="feedRepository">The feed repository.</param>
        /// <param name="pipeRepository">The pipe repository.</param>
        public DomainController(
            IAmACommandProcessor commandProcessor,
            IAmARepository<Domain> domainRepository,
            IAmARepository<Feed> feedRepository,
            IAmARepository<Pipe> pipeRepository)
        {
            _commandProcessor = commandProcessor;
            _domainRepository = domainRepository;
            _feedRepository = feedRepository;
            _pipeRepository = pipeRepository;
        }

        /// <summary>
        /// Gets the specified domain name.
        /// </summary>
        /// <param name="domainName">Name of the domain.</param>
        /// <returns>RestMSDomain.</returns>
        [HttpGet]
        [DomainNotFoundExceptionFilter]
        public RestMSDomain Get(string name)
        {
            var domainRetriever = new DomainRetriever(_domainRepository, _feedRepository, _pipeRepository);
            return domainRetriever.Retrieve(new Name(name));
        }

        /// <summary>
        /// We can post to a domain to add either a pipe, or a feed.
        /// This doesn't fit with WebApi which, unsuprisingly, can't overload on the type of resource sent to it.
        /// This is not an overload on the content-type, but handling different types of resource requests posted to the endpoint.
        /// As a result we have to manually read the body, and try to turn it into either a feed or pipe, to determine which is which.
        /// WebApi makes this a little tricky, because it only allows the content to be read ONCE
        /// </summary>
        /// <param name="name">Name of the domain.</param>
        /// <param name="feed">The feed.</param>
        /// <returns>HttpResponseMessage.</returns>
        [HttpPost]
        [DomainNotFoundExceptionFilter]
        [FeedAlreadyExistsExceptionFilter]
        public HttpResponseMessage Post(string name)
        {
            string content = Request.Content.ReadAsStringAsync().Result;
            //What is accept type? Get conversion strategy from factory. Does XML or JSON conversion
            var parser = ConversionStrategyFactory.CreateParser(Request.Headers.Accept);
            //Try to convert to pipe or feed using strategy
            var result = parser.Parse(content);
            //Return call to AddFeed or AddPipe
            if (result.Item1 == ParseResult.NewFeed)
            {
                return AddFeed(name, result.Item2);
            }
            else if (result.Item1 == ParseResult.NewPipe)
            {
                return AddPipe(name, result.Item3);
            }

            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Could not parse new feed or pipe from the body content");
        }

        private HttpResponseMessage AddFeed(string name, RestMSFeed feed)
        {
            var addFeedCommand = new AddFeedCommand(
                domainName: name,
                name: feed.Name,
                type: feed.Type,
                title: feed.Title);
            _commandProcessor.Send(addFeedCommand);

            return BuildDomainItemCreatedReponse(name);
        }

        private HttpResponseMessage AddPipe(string name, RestMSPipeNew pipe)
        {
            var newPipeCommand = new AddPipeCommand(name, pipe.Type, pipe.Title);
            _commandProcessor.Send(newPipeCommand);

            return BuildDomainItemCreatedReponse(name);
        }

        private HttpResponseMessage BuildDomainItemCreatedReponse(string domainName)
        {
            var domainRetriever = new DomainRetriever(_domainRepository, _feedRepository, _pipeRepository);
            var item = domainRetriever.Retrieve(new Name(domainName));
            var response = Request.CreateResponse<RestMSDomain>(HttpStatusCode.Created, item);
            response.Headers.Location = new Uri(item.Href);
            return response;
        }
    }
}
