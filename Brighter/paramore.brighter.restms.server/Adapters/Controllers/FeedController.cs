// ***********************************************************************
// Assembly         : paramore.brighter.restms.server
// Author           : ian
// Created          : 12-18-2014
//
// Last Modified By : ian
// Last Modified On : 12-31-2014
// ***********************************************************************
// <copyright file="FeedController.cs" company="">
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
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Web.Http;
using CacheCow.Server;
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
        readonly ICachingHandler cachingHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedController"/> class.
        /// </summary>
        /// <param name="commandProcessor">The command processor.</param>
        /// <param name="feedRepository">The feed repository.</param>
        /// <param name="cachingHandler">The caching handler, used to invalidate related resources</param>
        public FeedController(IAmACommandProcessor commandProcessor, IAmARepository<Feed> feedRepository, ICachingHandler cachingHandler)
        {
            this.commandProcessor = commandProcessor;
            this.feedRepository = feedRepository;
            this.cachingHandler = cachingHandler;
        }

        /// <summary>
        /// Gets the specified name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>RestMSFeed.</returns>
        [HttpGet]
        [FeedDoesNotExistExceptionFilter]
        public RestMSFeed Get(string name)
        {
            var feedRetriever = new FeedRetriever(feedRepository);
            return feedRetriever.Retrieve(new Name(name));
        }


        /// <summary>
        /// Deletes the specified name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>HttpResponseMessage.</returns>
        [HttpDelete]
        [FeedDoesNotExistExceptionFilter]
        public HttpResponseMessage Delete(string name)
        {
            var deleteFeedCommand = new DeleteFeedCommand(feedName: name);
            commandProcessor.Send(deleteFeedCommand);

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        /// <summary>
        /// Posts the message to feed.
        /// </summary>
        /// <param name="name">The feed name.</param>
        /// <param name="messageSpecification">The message specification.</param>
        /// <returns>HttpResponseMessage.</returns>
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
            return Request.CreateResponse<RestMSMessagePosted>(HttpStatusCode.OK, item);
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
