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

using System.Web.Http;
using Microsoft.Practices.ObjectBuilder2;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;
using paramore.brighter.restms.core.Ports.ViewModelRetrievers;

namespace paramore.brighter.restms.server.Adapters.Controllers
{
    public class DomainController : ApiController
    {
        readonly IAmACommandProcessor commandProcessor;
        readonly IAmARepository<Domain> domainRepository;
        readonly IAmARepository<Feed> feedRepository;

        public DomainController(IAmACommandProcessor commandProcessor, IAmARepository<Domain> domainRepository, IAmARepository<Feed> feedRepository)
        {
            this.commandProcessor = commandProcessor;
            this.domainRepository = domainRepository;
            this.feedRepository = feedRepository;
        }

        [Route("restms/domain/{domainName}")]
        [HttpGet]
        public RestMSDomain Get(string domainName)
        {
            var domainRetriever = new DomainRetriever(feedRepository, domainRepository);
            return domainRetriever.Retrieve(new Name(domainName));
        }

        [Route("restms/domain/{domainName}")]
        [HttpPost]
        public void Post(RestMSDomain domain)
        {
            //All ops idempotent - if exists with this name, update
            //Do we have any feeds: create them
            if (domain.Feeds.Length > 0)
            {
                domain.Feeds.ForEach(feed =>
                                     {
                                         var newFeedCommand = new NewFeedCommand(
                                             name: feed.Name,
                                             type: feed.Type,
                                             title: feed.Title);
                                         commandProcessor.Send(newFeedCommand);
                                     });
            }
            //Do we have any pipes create them
        }
    }
}
