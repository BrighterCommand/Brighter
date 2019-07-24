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
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.RESTMS.Exceptions;
using Paramore.Brighter.MessagingGateway.RESTMS.Model;

namespace Paramore.Brighter.MessagingGateway.RESTMS
{
    internal class Feed
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<Feed>);

        private readonly RestMSMessageGateway _gateway;

        /// <summary>
        /// The feed href
        /// </summary>
        public string FeedUri { get; set; }

        public Feed(RestMSMessageGateway gateway)
        {
            _gateway = gateway;
            FeedUri = null;
        }

        /// <summary>
        /// Ensures the feed exists.
        /// </summary>
        /// <param name="domain">The domain.</param>
        /// <exception cref="RestMSClientException"></exception>
        public void EnsureFeedExists(RestMSDomain domain)
        {
            var feedName = _gateway.Configuration.Feed.Name;
            _logger.Value.DebugFormat("Checking for existence of the feed {0} on the RestMS server: {1}", feedName, _gateway.Configuration.RestMS.Uri.AbsoluteUri);
            var isFeedDeclared = IsFeedDeclared(domain, feedName);
            if (!isFeedDeclared)
            {
                domain = CreateFeed(domain.Href, feedName);
                if (domain == null || !domain.Feeds.Any(feed => feed.Name == feedName))
                {
                    throw new RestMSClientException(string.Format("Unable to create feed {0} on the default domain; see log for errors", feedName));
                }
            }
            FeedUri = domain.Feeds.First(feed => feed.Name == feedName).Href;
        }

        private RestMSDomain CreateFeed(string domainUri, string name)
        {
            _logger.Value.DebugFormat("Creating the feed {0} on the RestMS server: {1}", name, _gateway.Configuration.RestMS.Uri.AbsoluteUri);
            var client = _gateway.Client();
            try
            {
                var response = client.SendAsync(_gateway.CreateRequest(
                    domainUri, _gateway.CreateEntityBody(
                        new RestMSFeed
                        {
                            Name = name,
                            Type = "Default",
                            Title = name
                        }))
                    ).Result;

                response.EnsureSuccessStatusCode();
                return _gateway.ParseResponse<RestMSDomain>(response);
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    _logger.Value.ErrorFormat("Threw exception adding Feed {0} to RestMS Server {1}", name, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error adding the Feed {0} to the RestMS server, see log for details", name));
            }
        }

        private bool IsFeedDeclared(RestMSDomain domain, string feedName)
        {
            return domain?.Feeds != null && domain.Feeds.Any(feed => feed.Name == feedName);
        }
    }
}
