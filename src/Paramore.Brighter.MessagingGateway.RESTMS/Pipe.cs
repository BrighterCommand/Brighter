


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
using Paramore.Brighter.MessagingGateway.RESTMS.Exceptions;
using Paramore.Brighter.MessagingGateway.RESTMS.Logging;
using Paramore.Brighter.MessagingGateway.RESTMS.Model;

namespace Paramore.Brighter.MessagingGateway.RESTMS
{
    internal class Pipe
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<Pipe>);

        private readonly RestMSMessageGateway _gateway;
        private readonly Join _join;

        public string PipeUri { get; private set; }

        public Pipe(RestMsMessageConsumer gateway, Feed feed)
        {
            _gateway = gateway;
            _join = new Join(gateway, feed);
        }


        public void EnsurePipeExists(string pipeTitle, string routingKey, RestMSDomain domain)
        {
            _logger.Value.DebugFormat("Checking for existence of the pipe {0} on the RestMS server: {1}", pipeTitle, _gateway.Configuration.RestMS.Uri.AbsoluteUri);
            var pipeExists = PipeExists(pipeTitle, domain);
            if (!pipeExists)
            {
                domain = CreatePipe(domain.Href, pipeTitle);
                if (domain == null || !domain.Pipes.Any(dp => dp.Title == pipeTitle))
                {
                    throw new RestMSClientException(string.Format("Unable to create pipe {0} on the default domain; see log for errors", pipeTitle));
                }

                _join.CreateJoin(domain.Pipes.First(p => p.Title == pipeTitle).Href, routingKey);
            }

            PipeUri = domain.Pipes.First(dp => dp.Title == pipeTitle).Href;
        }

        public RestMSPipe GetPipe()
        {
            /*TODO: Optimize this by using a repository approach with the repository checking for modification 
            through etag and serving existing version if not modified and grabbing new version if changed*/

            _logger.Value.DebugFormat("Getting the pipe from the RestMS server: {0}", PipeUri);
            var client = _gateway.Client();

            try
            {
                var response = client.GetAsync(PipeUri).Result;
                response.EnsureSuccessStatusCode();
                return _gateway.ParseResponse<RestMSPipe>(response);
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    _logger.Value.ErrorFormat("Threw exception getting Pipe {0} from RestMS Server {1}", PipeUri, exception.Message);
                }

                throw new RestMSClientException("Error retrieving the domain from the RestMS server, see log for details");
            }
        }

        private RestMSDomain CreatePipe(string domainUri, string title)
        {
            _logger.Value.DebugFormat("Creating the pipe {0} on the RestMS server: {1}", title, _gateway.Configuration.RestMS.Uri.AbsoluteUri);
            var client = _gateway.Client();
            try
            {
                var response = client.SendAsync(_gateway.CreateRequest(
                    domainUri, _gateway.CreateEntityBody(
                        new RestMSPipeNew
                        {
                            Type = "Default",
                            Title = title
                        })
                    )
                )
                .Result;

                response.EnsureSuccessStatusCode();
                return _gateway.ParseResponse<RestMSDomain>(response);
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    _logger.Value.ErrorFormat("Threw exception adding Pipe {0} to RestMS Server {1}", title, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error adding the Feed {0} to the RestMS server, see log for details", title));
            }
        }

        private bool PipeExists(string pipeTitle, RestMSDomain domain)
        {
            return domain?.Pipes != null && domain.Pipes.Any(p => p.Title == pipeTitle);
        }
    }
}