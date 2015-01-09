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
        string pipeUri;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestMsMessageConsumer"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RestMsMessageConsumer(ILog logger) : base(logger) {}

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
        public Message Receive(string queueName, string routingKey, int timeoutInMilliseconds)
        {
            Message message = null;
            try
            {
                var clientOptions = BuildClientOptions();
                EnsureFeedExists(GetDomain(clientOptions), clientOptions);
                EnsurePipeExists(queueName, routingKey, GetDomain(clientOptions), clientOptions);

                return ReadMessage(clientOptions);
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
            throw new NotImplementedException();
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

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Purge(string queueName)
        {
            throw new NotImplementedException();
        }

        RestMSJoin CreateJoin(string pipeUri, string routingKey, ClientOptions options)
        {
            Logger.DebugFormat("Creating the join with key {0} for pipe {1} on the RestMS server: {2}", routingKey, pipeUri, Configuration.RestMS.Uri.AbsoluteUri);
            var client = CreateClient(options);
            try
            {
                var response = client.SendAsync(
                    CreateRequest(
                        pipeUri,
                        CreateEntityBody(
                            new RestMSJoin
                            {
                                Address = routingKey,
                                Feed = FeedUri,
                                Type = "Default"
                            }
                            )
                        )
                    )
                                     .Result;

                response.EnsureSuccessStatusCode();
                var pipe = ParseResponse<RestMSPipe>(response);
                return pipe.Joins.FirstOrDefault();
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    Logger.ErrorFormat("Threw exception adding join with routingKey {0} to Pipe {1} on RestMS Server {2}", routingKey, pipeUri, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error adding the join with routingKey {0} to Pipe {1} to the RestMS server, see log for details", routingKey, pipeUri));
            }
        }

        RestMSDomain CreatePipe(string domainUri, string title, ClientOptions options)
        {
            Logger.DebugFormat("Creating the pipe {0} on the RestMS server: {1}", title, Configuration.RestMS.Uri.AbsoluteUri);
            var client = CreateClient(options);
            try
            {
                var response = client.SendAsync(
                    CreateRequest(
                        domainUri,
                        CreateEntityBody(
                            new RestMSPipeNew
                            {
                                Type = "Default",
                                Title = title
                            })
                        )
                    )
                                     .Result;

                response.EnsureSuccessStatusCode();
                return ParseResponse<RestMSDomain>(response);
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    Logger.ErrorFormat("Threw exception adding Pipe {0} to RestMS Server {1}", title, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error adding the Feed {0} to the RestMS server, see log for details", title));
            }
        }

        void EnsurePipeExists(string pipeTitle, string routingKey, RestMSDomain domain, ClientOptions options)
        {
            Logger.DebugFormat("Checking for existence of the pipe {0} on the RestMS server: {1}", pipeTitle, Configuration.RestMS.Uri.AbsoluteUri);
            var pipeExists = domain.Pipes.Any(p => p.Title == pipeTitle);
            if (!pipeExists)
            {
                domain = CreatePipe(domain.Href, pipeTitle, options);
                if (domain == null || !domain.Pipes.Any(dp => dp.Title == pipeTitle))
                {
                    throw new RestMSClientException(string.Format("Unable to create pipe {0} on the default domain; see log for errors", pipeTitle));
                }
                
                CreateJoin(domain.Pipes.First(p => p.Title == pipeTitle).Href, routingKey, options);
            }

            pipeUri = domain.Pipes.First(dp => dp.Title == pipeTitle).Href;
        }


        RestMSPipe GetPipe(ClientOptions options)
        {
            /*TODO: Optimize this by using a repository approach with the repository checking for modification 
            through etag and serving existing version if not modified and grabbing new version if changed*/

            Logger.DebugFormat("Getting the pipe from the RestMS server: {0}", pipeUri);
            var client = CreateClient(options);

            try
            {
                var response = client.GetAsync(pipeUri).Result;
                response.EnsureSuccessStatusCode();
                return ParseResponse<RestMSPipe>(response);
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    Logger.ErrorFormat("Threw exception getting Pipe {0} from RestMS Server {1}", pipeUri, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error retrieving the domain from the RestMS server, see log for details"));
            }

        }

        Message ReadMessage(ClientOptions options)
        {
            var pipe = GetPipe(options);
            return GetMessage(pipe.Messages.FirstOrDefault(), options);
        }

        Message GetMessage(RestMSMessageLink messageUri, ClientOptions options)
        {
            if (messageUri == null)
            {
                return new Message();
            }

            Logger.DebugFormat("Getting the message from the RestMS server: {0}", messageUri);
            var client = CreateClient(options);

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
                    Logger.ErrorFormat("Threw exception getting Pipe {0} from RestMS Server {1}", pipeUri, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error retrieving the domain from the RestMS server, see log for details"));
            }
        }
    }

}
