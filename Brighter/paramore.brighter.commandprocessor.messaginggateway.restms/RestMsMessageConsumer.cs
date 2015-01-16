// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.restms
// Author           : ian
// Created          : 12-18-2014
//
// Last Modified By : ian
// Last Modified On : 12-31-2014
// ***********************************************************************
// <copyright file="RestMsMessageConsumer.cs" company="">
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
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using Common.Logging;
using paramore.brighter.commandprocessor.messaginggateway.restms.Exceptions;
using paramore.brighter.commandprocessor.messaginggateway.restms.Model;
using Thinktecture.IdentityModel.Hawk.Client;

namespace paramore.brighter.commandprocessor.messaginggateway.restms
{
    /// <summary>
    /// Class RestMsMessageConsumer.
    /// </summary>
    public class RestMsMessageConsumer : RestMSMessageGateway, IAmAMessageConsumer
    {
        Pipe pipe;
        readonly Feed feed;
        readonly Domain domain; 

        /// <summary>
        /// Initializes a new instance of the <see cref="RestMsMessageConsumer" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RestMsMessageConsumer(ILog logger) : base(logger)
        {
            feed = new Feed(this);
            domain = new Domain(this); 
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Receives the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Message Receive(string queueName, string routingKey, int timeoutInMilliseconds = -1)
        {
            double timeout = timeoutInMilliseconds == -1 ? Timeout : timeoutInMilliseconds;

            try
            {
                var clientOptions = BuildClientOptions();
                feed.EnsureFeedExists(domain.GetDomain(clientOptions, timeout), clientOptions, timeout);
                pipe = new Pipe(this, feed);
                pipe.EnsurePipeExists(queueName, routingKey, domain.GetDomain(clientOptions, timeout), clientOptions, timeout);

                return ReadMessage(clientOptions, timeout);
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

            return null;
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Acknowledge(Message message)
        {
            var clientOptions = BuildClientOptions();
            var pipe = this.pipe.GetPipe(clientOptions, Timeout);
            DeleteMessage(pipe, message, clientOptions, Timeout);
        }

        /// <summary>
        /// Noes the of outstanding messages.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>System.Int32.</returns>
        public int NoOfOutstandingMessages(string queueName, string routingKey, int timeoutInMilliseconds = -1)
        {
            double timeout = timeoutInMilliseconds == -1 ? Timeout : timeoutInMilliseconds;

            try
            {
                var clientOptions = BuildClientOptions();
                this.pipe.EnsurePipeExists(queueName, routingKey, domain.GetDomain(clientOptions, timeout), clientOptions, timeout);
                var pipe = this.pipe.GetPipe(clientOptions, timeout);
                return pipe.Messages != null ? pipe.Messages.Count(): 0;
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
        /// Purges the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Purge(string queueName)
        {
            try
            {

                var clientOptions = BuildClientOptions();
                var pipe = this.pipe.GetPipe(clientOptions, Timeout);
                if (pipe != null && pipe.Messages != null)
                {
                    var message = pipe.Messages.FirstOrDefault();
                    do
                    {
                        if (message != null)
                        {
                            SendDeleteMessage(clientOptions, message, Timeout);
                        }
                         pipe = this.pipe.GetPipe(clientOptions, Timeout);   
                    } while (pipe.Messages != null && pipe.Messages.Any());
                }
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
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="requeue">if set to <c>true</c> [requeue].</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Reject(Message message, bool requeue)
        {
            throw new NotImplementedException();
        }


        void DeleteMessage(RestMSPipe pipe, Message message, ClientOptions options, double timeout)
        {
            if (pipe.Messages == null || !pipe.Messages.Any())
            {
                return;
            }

            var matchingMessage = pipe.Messages.FirstOrDefault(msg => msg.MessageId == message.Id.ToString());
            if (matchingMessage == null)
            {
                return;
            }

            Logger.DebugFormat("Deleting the message {0} from the pipe: {0}", message.Id, pipe.Href);
            SendDeleteMessage(options, matchingMessage, timeout);
        }

        Message GetMessage(RestMSMessageLink messageUri, ClientOptions options, double timeout)
        {
            if (messageUri == null)
            {
                return new Message();
            }

            Logger.DebugFormat("Getting the message from the RestMS server: {0}", messageUri);
            var client = CreateClient(options, timeout);

            try
            {
                var response = client.GetAsync(messageUri.Href).Result;
                response.EnsureSuccessStatusCode();
                var pipeMessage = ParseResponse<RestMSMessage>(response);
                return RestMSMessageCreator.CreateMessage(pipeMessage);
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    Logger.ErrorFormat("Threw exception getting Pipe {0} from RestMS Server {1}", pipe.PipeUri, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error retrieving the domain from the RestMS server, see log for details"));
            }
        }

        Message ReadMessage(ClientOptions options, double timeout)
        {
            var pipe = this.pipe.GetPipe(options, timeout);
            return GetMessage(pipe.Messages != null ? pipe.Messages.FirstOrDefault() : null, options, timeout);
        }

        void SendDeleteMessage(ClientOptions options, RestMSMessageLink matchingMessage, double timeout)
        {
            var client = CreateClient(options, timeout);
            var response = client.DeleteAsync(matchingMessage.Href).Result;
            response.EnsureSuccessStatusCode();
        }
    }

}
