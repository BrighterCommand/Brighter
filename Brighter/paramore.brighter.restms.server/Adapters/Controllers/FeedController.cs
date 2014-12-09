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
using System.Collections.Specialized;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Web.Http;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Extensions;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;
using paramore.brighter.restms.core.Ports.ViewModelRetrievers;
using paramore.brighter.restms.server.Adapters.Filters;

namespace paramore.brighter.restms.server.Adapters.Controllers
{
    /// <summary>
    /// Class FeedController.
    /// </summary>
    [Authorize]
    public class FeedController : ApiController
    {
        readonly IAmACommandProcessor commandProcessor;
        readonly IAmARepository<Feed> feedRepository;

        public FeedController(IAmACommandProcessor commandProcessor, IAmARepository<Feed> feedRepository)
        {
            this.commandProcessor = commandProcessor;
            this.feedRepository = feedRepository;
        }

        [HttpGet]
        [FeedDoesNotExistExceptionFilter]
        public RestMSFeed Get(string name)
        {
            //TODO: Get needs Last Modified and ETag
            var feedRetriever = new FeedRetriever(feedRepository);
            return feedRetriever.Retrieve(new Name(name));
        }

       
        [HttpDelete]
        [FeedDoesNotExistExceptionFilter]
        public HttpResponseMessage Delete(string name)
        {
            //TODO: Should support conditional DELETE based on ETag
            var deleteFeedCommand = new DeleteFeedCommand(feedName: name);
            commandProcessor.Send(deleteFeedCommand);

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [HttpPost]
        [FeedDoesNotExistExceptionFilter]
        public HttpResponseMessage PostMessageToFeed(string name, RestMSMessage messageSpecification)
        {
            var addMessageToFeedCommand = new AddMessageToFeedCommand(
                name,
                messageSpecification.Address,
                messageSpecification.ReplyTo ?? "",
                GetHeadersFromMessage(messageSpecification),
                GetAttachmentFromMessage(messageSpecification.Content)
                );

            commandProcessor.Send(addMessageToFeedCommand);

            var item =new RestMSMessagePosted() {Count = addMessageToFeedCommand.MatchingJoins};
            var response = Request.CreateResponse<RestMSMessagePosted>(HttpStatusCode.OK, item);
            return response;
        }

        Attachment GetAttachmentFromMessage(RestMSMessageContent content)
        {
            return Attachment.CreateAttachmentFromString(
                content.Value,
                Guid.NewGuid().ToString(),
                Encoding.GetEncoding(content.Encoding),
                content.Type);
        }

        NameValueCollection GetHeadersFromMessage(RestMSMessage messageSpecification)
        {
            var headers = new NameValueCollection();
            if (messageSpecification.Headers != null)
                messageSpecification.Headers.Each(header => headers.Add(header.Name, header.Value));
            return headers;
        }
    }
}
