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
    public class DomainController : ApiController
    {
        readonly IAmACommandProcessor commandProcessor;
        readonly IAmARepository<Domain> domainRepository;
        readonly IAmARepository<Feed> feedRepository;
        readonly IAmARepository<Pipe> pipeRepository;

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

        [Route("restms/domain/{domainName}")]
        [HttpGet]
        [DomainNotFoundExceptionFilter]
        public RestMSDomain Get(string domainName)
        {
            //TODO: Get needs Last Modified and ETag.
            var domainRetriever = new DomainRetriever(domainRepository, feedRepository, pipeRepository);
            return domainRetriever.Retrieve(new Name(domainName));
        }

        [Route("restms/domain/{domainName}")]
        [HttpPost]
        [DomainNotFoundExceptionFilter]
        [FeedAlreadyExistsExceptionFilter]
        public HttpResponseMessage Post(RestMSDomain domain)
        {
            //we only want to one of feed or pipe, make sure request does not include more
            //TODO: Wehen we return 201 we should also return a Location header for the new resource
            var anyFeeds = domain.Feeds.Any();
            var morethanOneFeed = domain.Feeds.Length > 1;

            if (anyFeeds && morethanOneFeed)
            {
                return Request.CreateErrorResponse(
                    HttpStatusCode.BadRequest, 
                    string.Format("You tried to create {0} feeds, please just create one at a time", domain.Feeds.Length));

            }
            
            if (anyFeeds) 
            {
                var feed = domain.Feeds.First();
                var newFeedCommand = new NewFeedCommand(
                    domainName: domain.Name,
                    name: feed.Name,
                    type: feed.Type,
                    title: feed.Title);
                commandProcessor.Send(newFeedCommand);

                return Request.CreateResponse(HttpStatusCode.Accepted);
            }


            //Do we have any pipes create them

            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Unknown content in POST");
        }
    }
}
