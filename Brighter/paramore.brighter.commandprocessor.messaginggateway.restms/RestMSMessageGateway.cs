// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.restms
// Author           : ian
// Created          : 01-02-2015
//
// Last Modified By : ian
// Last Modified On : 01-02-2015
// ***********************************************************************
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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.restms.MessagingGatewayConfiguration;
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
        private ThreadLocal<HttpClient> _client;
        private readonly double _timeout;

        /// <summary>
        /// The logger
        /// </summary>
        public readonly ILog Logger;
        /// <summary>
        /// The configuration
        /// </summary>
        public readonly RestMSMessagingGatewayConfiguration Configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// <param name="configuration">The configuration to use with RestMS.</param>
        /// </summary>
        public RestMSMessageGateway(RestMSMessagingGatewayConfiguration configuration) 
            :this(configuration, LogProvider.For<RestMSMessageGateway>())
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// Use this if you need to provide the logger, for example for testing
        /// </summary>
        /// <param name="configuration">The configuration to use with RestMS.</param>
        /// <param name="logger">The logger.</param>
        public RestMSMessageGateway(RestMSMessagingGatewayConfiguration configuration, ILog logger)
        {
            Configuration = configuration; 
            Logger = logger;
            _timeout = Configuration.RestMS.Timeout;
        }

        public HttpClient Client()
        {
            _client = new ThreadLocal<HttpClient>(() => CreateClient(BuildClientOptions(), _timeout));
            return _client.Value;
        }

        /// <summary>
        /// Creates the entity body.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="feed">The feed.</param>
        /// <returns>StringContent.</returns>
        public StringContent CreateEntityBody<T>(T feed) where T : class, new()
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
        public HttpRequestMessage CreateRequest(string uri, StringContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(@"text/xml");
            return request;
        }

        /// <summary>
        /// Parses the response.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="response">The response.</param>
        /// <returns>T.</returns>
        /// <exception cref="ResultParserException"></exception>
        public T ParseResponse<T>(HttpResponseMessage response) where T : class, new()
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

        /// <summary>
        /// Builds the client options.
        /// </summary>
        /// <returns>ClientOptions.</returns>
        private ClientOptions BuildClientOptions()
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


        private HttpClient CreateClient(ClientOptions options, double timeout)
        {
            var handler = new HawkValidationHandler(options);
            var requestHandler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
            };
            handler.InnerHandler = requestHandler;
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(timeout),
                
            };
            client.DefaultRequestHeaders.CacheControl.MustRevalidate = true;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
            return client;
        }
    }
}