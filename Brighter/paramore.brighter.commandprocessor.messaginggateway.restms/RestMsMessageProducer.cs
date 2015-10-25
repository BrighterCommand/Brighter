// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.restms
// Author           : ian
// Created          : 12-18-2014
//
// Last Modified By : ian
// Last Modified On : 12-31-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright � 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.restms.Exceptions;
using paramore.brighter.commandprocessor.messaginggateway.restms.Model;
using paramore.brighter.commandprocessor.messaginggateway.restms.Parsers;

namespace paramore.brighter.commandprocessor.messaginggateway.restms
{
    /// <summary>
    /// Class RestMsMessageProducer.
    /// </summary>
    public class RestMsMessageProducer : RestMSMessageGateway, IAmAMessageProducer
    {
        private readonly Feed _feed;
        private readonly Domain _domain;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestMsMessageProducer"/> class.
        /// </summary>
        public RestMsMessageProducer()
            : this(LogProvider.GetCurrentClassLogger()) 
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="RestMsMessageProducer"/> class.
        /// Use this if you need to inject the logger, for example for testing
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RestMsMessageProducer(ILog logger) : base(logger)
        {
            _feed = new Feed(this);
            _domain = new Domain(this);
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Send(Message message)
        {
            try
            {
                _feed.EnsureFeedExists(_domain.GetDomain());
                SendMessage(Configuration.Feed.Name, message);
            }
            catch (RestMSClientException rmse)
            {
                Logger.ErrorFormat("Error sending to the RestMS server: {0}", rmse.ToString());
                throw;
            }
            catch (HttpRequestException he)
            {
                Logger.ErrorFormat("HTTP error on request to the RestMS server: {0}", he.ToString());
                throw;
            }
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~RestMsMessageProducer()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
        }

        private StringContent CreateMessageEntityBody(Message message)
        {
            var messageToSend = new RestMSMessage
            {
                Address = message.Header.Topic,
                MessageId = message.Header.Id.ToString(),
                Content = new RestMSMessageContent
                {
                    Value = message.Body.Value,
                    Type = MediaTypeNames.Text.Plain,
                    Encoding = Encoding.ASCII.WebName
                }
            };

            var messageHeaders = new List<RestMSMessageHeader> { new RestMSMessageHeader { Name = "MessageType", Value = message.Header.MessageType.ToString() } };
            messageHeaders.AddRange(message.Header.Bag.Select(customHeader => new RestMSMessageHeader { Name = customHeader.Key, Value = Encoding.ASCII.GetString((byte[])customHeader.Value) }));

            messageToSend.Headers = messageHeaders.ToArray();

            string messageContent;
            if (!XmlRequestBuilder.TryBuild(messageToSend, out messageContent)) return null;
            return new StringContent(messageContent);
        }

        private RestMSMessagePosted SendMessage(string feedName, Message message)
        {
            try
            {
                if (_feed.FeedUri == null)
                {
                    throw new RestMSClientException(string.Format("The feed href for feed {0} has not been initialized", feedName));
                }

                var client = Client();
                var response = client.SendAsync(
                    CreateRequest(
                        _feed.FeedUri,
                        CreateMessageEntityBody(message)
                     )
                 ).Result;

                response.EnsureSuccessStatusCode();
                return ParseResponse<RestMSMessagePosted>(response);
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    Logger.ErrorFormat("Threw exception sending message to feed {0} with Id {1} due to {2}", feedName, message.Header.Id, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error sending message to feed {0} with Id {1} , see log for details", feedName, message.Header.Id));
            }
        }
    }
}
