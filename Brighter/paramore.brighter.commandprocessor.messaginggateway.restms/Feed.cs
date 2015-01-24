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
using paramore.brighter.commandprocessor.messaginggateway.restms.Exceptions;
using paramore.brighter.commandprocessor.messaginggateway.restms.Model;
using Thinktecture.IdentityModel.Hawk.Client;

namespace paramore.brighter.commandprocessor.messaginggateway.restms
{
    internal class Feed
    {
        readonly RestMSMessageGateway gateway;

        /// <summary>
        /// The feed href
        /// </summary>
        public string FeedUri { get; set; }

        public Feed(RestMSMessageGateway gateway)
        {
            this.gateway = gateway;
            FeedUri = null;
        }

        /// <summary>
        /// Ensures the feed exists.
        /// </summary>
        /// <param name="domain">The domain.</param>
        /// <param name="options">The options.</param>
        /// <exception cref="RestMSClientException"></exception>
        public void EnsureFeedExists(RestMSDomain domain)
        {
            var feedName = gateway.Configuration.Feed.Name;
            gateway.Logger.DebugFormat("Checking for existence of the feed {0} on the RestMS server: {1}", feedName, gateway.Configuration.RestMS.Uri.AbsoluteUri);
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

        RestMSDomain CreateFeed(string domainUri, string name)
        {
            gateway.Logger.DebugFormat("Creating the feed {0} on the RestMS server: {1}", name, gateway.Configuration.RestMS.Uri.AbsoluteUri);
            var client = gateway.Client();
            try
            {
                var response = client.SendAsync(gateway.CreateRequest(
                    domainUri, gateway.CreateEntityBody(
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
                return gateway.ParseResponse<RestMSDomain>(response);
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    gateway.Logger.ErrorFormat("Threw exception adding Feed {0} to RestMS Server {1}", name, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error adding the Feed {0} to the RestMS server, see log for details", name));
            }
        }

        bool IsFeedDeclared(RestMSDomain domain, string feedName)
        {
            return domain != null && domain.Feeds !=null && domain.Feeds.Any(feed => feed.Name == feedName);
        }
    }
}