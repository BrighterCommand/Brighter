// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.restms
// Author           : ian
// Created          : 12-18-2014
//
// Last Modified By : ian
// Last Modified On : 12-31-2014
// ***********************************************************************
// <copyright file="RestMsMessageProducer.cs" company="">
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
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using paramore.brighter.commandprocessor.messaginggateway.restms.Exceptions;
using paramore.brighter.commandprocessor.messaginggateway.restms.MessagingGatewayConfiguration;
using paramore.brighter.commandprocessor.messaginggateway.restms.Model;
using paramore.brighter.commandprocessor.messaginggateway.restms.Parsers;
using Thinktecture.IdentityModel.Hawk.Client;
using Thinktecture.IdentityModel.Hawk.Core;
using Thinktecture.IdentityModel.Hawk.Core.Helpers;

namespace paramore.brighter.commandprocessor.messaginggateway.restms
{
    /// <summary>
    /// Class RestMsMessageProducer.
    /// </summary>
    public class RestMsMessageProducer : IAmAMessageProducer
    {
        readonly ILog logger;
        readonly RestMSMessagingGatewayConfigurationSection configuration;
        string feedHref = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestMsMessageProducer"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RestMsMessageProducer(ILog logger)
        {
            this.logger = logger;
            configuration = RestMSMessagingGatewayConfigurationSection.GetConfiguration();
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public Task Send(Message message)
        {
            var tcs = new TaskCompletionSource<object>();

            try
            {
                logger.DebugFormat("Getting the default domain from the RestMS server: {0}", configuration.RestMS.Uri.AbsoluteUri);
                var clientOptions = BuildClientOptions();
                EnsureFeedExists(GetDefaultDomain(clientOptions), clientOptions);
                SendMessage(configuration.Feed.Name, message, clientOptions);
            }
            catch (RestMSClientException rmse)
            {
                logger.ErrorFormat("Error sending to the RestMS server: {0}", rmse.ToString());
                tcs.SetException(rmse);
                throw;
            }
            catch (HttpRequestException he)
            {
                logger.ErrorFormat("HTTP error on request to the RestMS server: {0}", he.ToString());
                tcs.SetException(he);
                throw;
            }

            tcs.SetResult(new object());
            return tcs.Task;
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

        void Dispose(bool disposing)
        {
            
        }

        ClientOptions BuildClientOptions()
        {
            var credential = new Credential()
            {
                Id = configuration.RestMS.Id, 
                Algorithm = SupportedAlgorithms.SHA256,
                User = configuration.RestMS.User,
                Key = Convert.FromBase64String(configuration.RestMS.Key) 
            };

            var options = new ClientOptions()
            {
                CredentialsCallback = () => credential
            };
            return options;
        }

        RestMSDomain CreateFeed(string name, ClientOptions options)
        {
            var client = CreateClient(options);
            try
            {
                var feed = new RestMSFeed
                {
                    Name = name,
                    Type = "Default",
                    Title = name
                };

                string feedRequest;
                if (!XmlRequestBuilder.TryBuild(feed, out feedRequest)) return null;
                var content = new StringContent(feedRequest);
                var request = new HttpRequestMessage(HttpMethod.Post, configuration.RestMS.Uri.AbsoluteUri) {Content = content};
                var response = client.SendAsync(request).Result;
                response.EnsureSuccessStatusCode();
                return ParseResponse<RestMSDomain>(response);
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    logger.ErrorFormat("Threw exception adding Feed {0} to RestMS Server {1}", name, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error adding the Feed {0} to the RestMS server, see log for details", name));
            }
        }

        HttpClient CreateClient(ClientOptions options)
        {
            var handler = new HawkValidationHandler(options);
            var client = HttpClientFactory.Create(handler);
            client.Timeout = TimeSpan.FromMilliseconds(30000);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
            return client;
        }

        void EnsureFeedExists(RestMSDomain domain, ClientOptions options)
        {
            var isFeedDeclared = domain.Feeds.Any(feed => feed.Name == configuration.Feed.Name);
            if (!isFeedDeclared)
            {
                domain = CreateFeed(configuration.Feed.Name, options);
                if (domain == null || !domain.Feeds.Any(feed => feed.Name == configuration.Feed.Name))
                {
                    throw new RestMSClientException(string.Format("Unable to create feed {0} on the default domain; see log for errors", configuration.Feed.Name));
                }
            }
            feedHref = domain.Feeds.Where(feed => feed.Name == configuration.Feed.Name).First().Href;
        }

        RestMSDomain GetDefaultDomain(ClientOptions options)
         {
            /*TODO: Optimize this by using a repository approach with the repository checking for modification 
            through etag and serving existing version if not modified and grabbing new version if changed*/

            var client = CreateClient(options);

             try
             {
                 var response = client.GetAsync(configuration.RestMS.Uri).Result;
                 response.EnsureSuccessStatusCode();
                 return ParseResponse<RestMSDomain>(response);
             }
             catch (AggregateException ae)
             {
                 foreach (var exception in ae.Flatten().InnerExceptions)
                 {
                     logger.ErrorFormat("Threw exception getting Domain from RestMS Server {0}", exception.Message);
                 }

                 throw new RestMSClientException(string.Format("Error retrieving the domain from the RestMS server, see log for details"));
             }
         }

        T ParseResponse<T>(HttpResponseMessage response) where T : class, new()
        {
            var entityBody = response.Content.ReadAsStringAsync().Result;
            T domainObject;
            if (!XmlResultParser.TryParse(entityBody, out domainObject))
            {
                var errorString = string.Format("Could not parse entity body as a domain => {0}", entityBody);
                logger.ErrorFormat(errorString);
                throw new ResultParserException(errorString);
            }
            return domainObject;
        }

        RestMSMessagePosted SendMessage(string feedName, Message message, ClientOptions options)
        {
            try
            {
                if (feedHref == null)
                {
                    throw new RestMSClientException(string.Format("The feed href for feed {0} has not been initialized", feedName));
                }

                var client = CreateClient(options);

                var messageToSend = new RestMSMessage()
                {
                    Feed = feedName,
                    Address = message.Header.Topic,
                    MessageId = message.Header.Id.ToString(),
                    Content = new RestMSMessageContent
                    {
                        Value = message.Body.Value,
                        Type = MediaTypeNames.Text.Plain,
                        Encoding = Encoding.ASCII.WebName
                    }
                };

                string messageContent;
                if (!XmlRequestBuilder.TryBuild(messageToSend, out messageContent)) return null;
                var content = new StringContent(messageContent);
                var request = new HttpRequestMessage(HttpMethod.Post, feedHref) {Content = content};
                var response = client.SendAsync(request).Result;
                response.EnsureSuccessStatusCode();
                return ParseResponse<RestMSMessagePosted>(response);
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    logger.ErrorFormat("Threw exception sending message to feed {0} with Id {1} due to {2}", feedName, message.Header.Id, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error sending message to feed {0} with Id {1} , see log for details", feedName, message.Header.Id));
            }

        }

    }
}