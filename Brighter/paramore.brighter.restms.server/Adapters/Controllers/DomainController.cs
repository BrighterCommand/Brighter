// ***********************************************************************
// Assembly         : paramore.brighter.restms.server
// Author           : ian
// Created          : 11-05-2014
//
// Last Modified By : ian
// Last Modified On : 11-06-2014
// ***********************************************************************
// <copyright file="DomainController.cs" company="">
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
using System.Linq;
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

namespace paramore.brighter.restms.server.Adapters.Controllers
{
    /// <summary>
    /// Class DomainController.
    /// </summary>
    public class DomainController : ApiController
    {
        readonly IAmACommandProcessor commandProcessor;
        readonly IAmARepository<Domain> domainRepository;
        readonly IAmARepository<Feed> feedRepository;
        readonly IAmARepository<Pipe> pipeRepository;

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
            this.commandProcessor = commandProcessor;
            this.domainRepository = domainRepository;
            this.feedRepository = feedRepository;
            this.pipeRepository = pipeRepository;
        }

        /// <summary>
        /// Gets the specified domain name.
        /// </summary>
        /// <param name="domainName">Name of the domain.</param>
        /// <returns>RestMSDomain.</returns>
        [Route("restms/domain/{domainName}")]
        [HttpGet]
        [DomainNotFoundExceptionFilter]
        public RestMSDomain Get(string domainName)
        {
            //TODO: Get needs Last Modified and ETag.
            var domainRetriever = new DomainRetriever(domainRepository, feedRepository, pipeRepository);
            return domainRetriever.Retrieve(new Name(domainName));
        }

        /// <summary>
        /// Add a feed.
        /// </summary>
        /// <param name="domainName">Name of the domain.</param>
        /// <param name="feed">The feed.</param>
        /// <returns>HttpResponseMessage.</returns>
        [Route("restms/domain/{domainName}")]
        [HttpPost]
        [DomainNotFoundExceptionFilter]
        [FeedAlreadyExistsExceptionFilter]
        public HttpResponseMessage PostFeed(string domainName, RestMSFeed feed)
        {
     
            var addFeedCommand = new AddFeedCommand(
                domainName: domainName,
                name: feed.Name,
                type: feed.Type,
                title: feed.Title);
            commandProcessor.Send(addFeedCommand);

            return BuildDomainItemCreatedReponse(domainName);
        }

        /// <summary>
        /// Add a new pipe.
        /// </summary>
        /// <param name="domainName">Name of the domain.</param>
        /// <param name="pipe">The pipe.</param>
        /// <returns>HttpResponseMessage.</returns>
        [Route("restms/domain/{domainName}")]
        [HttpPost]
        [DomainNotFoundExceptionFilter]
        [PipeDoesNotExistExceptionFilter]
        public HttpResponseMessage PostPipe(string domainName, RestMSPipeNew pipe)
        {
            var newPipeCommand = new AddPipeCommand(domainName, pipe.Type, pipe.Title);
            commandProcessor.Send(newPipeCommand);

            return BuildDomainItemCreatedReponse(domainName);
        }

        HttpResponseMessage BuildDomainItemCreatedReponse(string domainName)
        {
            var domainRetriever = new DomainRetriever(domainRepository, feedRepository, pipeRepository);
            var item = domainRetriever.Retrieve(new Name(domainName));
            var response = Request.CreateResponse<RestMSDomain>(HttpStatusCode.Created, item);
            response.Headers.Location = new Uri(item.Href);
            return response;
        }
    }
}
