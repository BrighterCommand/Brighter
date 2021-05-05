﻿#region Licence
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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.RESTMS.MessagingGatewayConfiguration;
using Paramore.Brighter.MessagingGateway.RESTMS.Parsers;

namespace Paramore.Brighter.MessagingGateway.RESTMS
{
    /// <summary>
    /// Class RestMSMessageGateway.
    /// </summary>
    public class RestMSMessageGateway
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RestMSMessageGateway>();

        private ThreadLocal<HttpClient> _client;
        private readonly double _timeout;

        /// <summary>
        /// The configuration
        /// </summary>
        public readonly RestMSMessagingGatewayConfiguration Configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// <param name="configuration">The configuration to use with RestMS.</param>
        /// </summary>
        public RestMSMessageGateway(RestMSMessagingGatewayConfiguration configuration) 
        {
            Configuration = configuration; 
            _timeout = Configuration.RestMS.Timeout;
        }

        public HttpClient Client()
        {
            _client = new ThreadLocal<HttpClient>(() => CreateClient(_timeout));
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
                var errorString = $"Could not parse entity body as a domain => {entityBody}";
                s_logger.LogError("Could not parse entity body as a domain => {Request}", entityBody);
                throw new ResultParserException(errorString);
            }
            return domainObject;
        }

        private HttpClient CreateClient( double timeout)
        {

            var client = new HttpClient()
            {
                Timeout = TimeSpan.FromMilliseconds(timeout),
                
            };
            client.DefaultRequestHeaders.CacheControl.MustRevalidate = true;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
            return client;
        }
    }
}
