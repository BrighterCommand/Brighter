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
        /// The feed href{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
        /// </summary>
        protected string FeedHref = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RestMSMessageGateway(ILog logger)
        {
            Configuration = RestMSMessagingGatewayConfigurationSection.GetConfiguration();
            Logger = logger;
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
        /// <returns>HttpClient.</returns>
        protected HttpClient CreateClient(ClientOptions options)
        {
            var handler = new HawkValidationHandler(options);
            var client = HttpClientFactory.Create(handler);
            client.Timeout = TimeSpan.FromMilliseconds(30000);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
            return client;
        }

        RestMSDomain CreateFeed(string name, ClientOptions options)
        {
            Logger.DebugFormat("Creating the feed {0} on the RestMS server: {1}", name, Configuration.RestMS.Uri.AbsoluteUri);
            var client = CreateClient(options);
            try
            {
                var response = client.SendAsync(
                    CreateRequest(
                        CreateFeedEntityBody(
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

        StringContent CreateFeedEntityBody(RestMSFeed feed)
        {
            string feedRequest;
            if (!XmlRequestBuilder.TryBuild(feed, out feedRequest)) return null;
            return new StringContent(feedRequest);
        }

        /// <summary>
        /// Creates the request.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <returns>HttpRequestMessage.</returns>
        protected HttpRequestMessage CreateRequest(StringContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, FeedHref) {Content = content};
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Text.Xml);
            return request;
        }

        /// <summary>
        /// Ensures the feed exists.
        /// </summary>
        /// <param name="domain">The domain.</param>
        /// <param name="options">The options.</param>
        /// <exception cref="RestMSClientException"></exception>
        protected void EnsureFeedExists(RestMSDomain domain, ClientOptions options)
        {
            var feedName = Configuration.Feed.Name;
            Logger.DebugFormat("Checking for existence of the feed {0} on the RestMS server: {1}", feedName, Configuration.RestMS.Uri.AbsoluteUri);
            var isFeedDeclared = domain.Feeds.Any(feed => feed.Name == feedName);
            if (!isFeedDeclared)
            {
                domain = CreateFeed(Configuration.Feed.Name, options);
                if (domain == null || !domain.Feeds.Any(feed => feed.Name == feedName))
                {
                    throw new RestMSClientException(string.Format("Unable to create feed {0} on the default domain; see log for errors", feedName));
                }
            }
            FeedHref = domain.Feeds.First(feed => feed.Name == feedName).Href;
        }

        /// <summary>
        /// Gets the default domain.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>RestMSDomain.</returns>
        /// <exception cref="RestMSClientException"></exception>
        protected RestMSDomain GetDefaultDomain(ClientOptions options)
        {

            Logger.DebugFormat("Getting the default domain from the RestMS server: {0}", Configuration.RestMS.Uri.AbsoluteUri);
            var client = CreateClient(options);

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