// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.restms
// Author           : ian
// Created          : 01-02-2015
//
// Last Modified By : ian
// Last Modified On : 01-02-2015
// ***********************************************************************
// <copyright file="RestMSMessageGateway.cs" company="">
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
    /// Class RestMSMessageGateway.
    /// </summary>
    public class RestMSMessageGateway
    {
        /// <summary>
        /// The logger
        /// </summary>
        protected readonly ILog Logger;
        /// <summary>
        /// The configuration
        /// </summary>
        protected readonly RestMSMessagingGatewayConfigurationSection Configuration;

        /// <summary>
        /// The feed href
        /// </summary>
        protected string FeedUri = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RestMSMessageGateway(ILog logger)
        {
            Configuration = RestMSMessagingGatewayConfigurationSection.GetConfiguration();
            Logger = logger;
        }

        protected double Timeout
        {
            get
            {
                return Convert.ToDouble(Configuration.RestMS.Timeout);
            }
        }

        /// <summary>
        /// Builds the client options.
        /// </summary>
        /// <returns>ClientOptions.</returns>
        protected ClientOptions BuildClientOptions()
        {
            var credential = new Credential()
            {
                Id = Configuration.RestMS.Id,
                Algorithm = SupportedAlgorithms.SHA256,
                User = Configuration.RestMS.User,
                Key = Convert.FromBase64String(Configuration.RestMS.Key)
            };

            var options = new ClientOptions()
            {
                CredentialsCallback = () => credential
            };
            return options;
        }

        /// <summary>
        /// Creates the client.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="timeout">The timeout value for this call</param>
        /// <returns>HttpClient.</returns>
        protected HttpClient CreateClient(ClientOptions options, double timeout)
        {
            var handler = new HawkValidationHandler(options);
            var client = HttpClientFactory.Create(handler);
            client.Timeout = TimeSpan.FromMilliseconds(timeout);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
            return client;
        }

        /// <summary>
        /// Creates the entity body.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="feed">The feed.</param>
        /// <returns>StringContent.</returns>
        protected StringContent CreateEntityBody<T>(T feed) where T : class, new()
        {
            string feedRequest;
            if (!XmlRequestBuilder.TryBuild(feed, out feedRequest)) return null;
            return new StringContent(feedRequest);
        }

        RestMSDomain CreateFeed(string domainUri, string name, ClientOptions options, double timeout)
        {
            Logger.DebugFormat("Creating the feed {0} on the RestMS server: {1}", name, Configuration.RestMS.Uri.AbsoluteUri);
            var client = CreateClient(options, timeout);
            try
            {
                var response = client.SendAsync(
                    CreateRequest(
                        domainUri, 
                        CreateEntityBody(
                            new RestMSFeed
                            {
                                Name = name,
                                Type = "Default",
                                Title = name
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
                    Logger.ErrorFormat("Threw exception adding Feed {0} to RestMS Server {1}", name, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error adding the Feed {0} to the RestMS server, see log for details", name));
            }
        }

        /// <summary>
        /// Creates the request.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <returns>HttpRequestMessage.</returns>
        protected HttpRequestMessage CreateRequest(string uri, StringContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, uri) {Content = content};
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Text.Xml);
            return request;
        }

        /// <summary>
        /// Ensures the feed exists.
        /// </summary>
        /// <param name="domain">The domain.</param>
        /// <param name="options">The options.</param>
        /// <exception cref="RestMSClientException"></exception>
        protected void EnsureFeedExists(RestMSDomain domain, ClientOptions options, double timeout)
        {
            /*TODO: Optimize this by using a repository approach with the repository checking for modification 
            through etag and serving existing version if not modified and grabbing new version if changed*/
            var feedName = Configuration.Feed.Name;
            Logger.DebugFormat("Checking for existence of the feed {0} on the RestMS server: {1}", feedName, Configuration.RestMS.Uri.AbsoluteUri);
            var isFeedDeclared = IsFeedDeclared(domain, feedName);
            if (!isFeedDeclared)
            {
                domain = CreateFeed(domain.Href, feedName, options, timeout);
                if (domain == null || !domain.Feeds.Any(feed => feed.Name == feedName))
                {
                    throw new RestMSClientException(string.Format("Unable to create feed {0} on the default domain; see log for errors", feedName));
                }
            }
            FeedUri = domain.Feeds.First(feed => feed.Name == feedName).Href;
        }


        /// <summary>
        /// Gets the default domain.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>RestMSDomain.</returns>
        /// <exception cref="RestMSClientException"></exception>
        protected RestMSDomain GetDomain(ClientOptions options, double timeout)
        {
            /*TODO: Optimize this by using a repository approach with the repository checking for modification 
            through etag and serving existing version if not modified and grabbing new version if changed*/
            
            Logger.DebugFormat("Getting the default domain from the RestMS server: {0}", Configuration.RestMS.Uri.AbsoluteUri);
            var client = CreateClient(options, timeout);

            try
            {
                var response = client.GetAsync(Configuration.RestMS.Uri).Result;
                response.EnsureSuccessStatusCode();
                return ParseResponse<RestMSDomain>(response);
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    Logger.ErrorFormat("Threw exception getting Domain from RestMS Server {0}", exception.Message);
                }

                throw new RestMSClientException(string.Format("Error retrieving the domain from the RestMS server, see log for details"));
            }
        }

        bool IsFeedDeclared(RestMSDomain domain, string feedName)
        {
            return domain != null && domain.Feeds !=null && domain.Feeds.Any(feed => feed.Name == feedName);
        }

        /// <summary>
        /// Parses the response.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="response">The response.</param>
        /// <returns>T.</returns>
        /// <exception cref="ResultParserException"></exception>
        protected T ParseResponse<T>(HttpResponseMessage response) where T : class, new()
        {
            var entityBody = response.Content.ReadAsStringAsync().Result;
            T domainObject;
            if (!XmlResultParser.TryParse(entityBody, out domainObject))
            {
                var errorString = string.Format("Could not parse entity body as a domain => {0}", entityBody);
                Logger.ErrorFormat(errorString);
                throw new ResultParserException(errorString);
            }
            return domainObject;
        }
    }
}