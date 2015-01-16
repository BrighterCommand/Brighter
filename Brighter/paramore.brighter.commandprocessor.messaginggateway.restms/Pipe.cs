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
using paramore.brighter.commandprocessor.messaginggateway.restms.Exceptions;
using paramore.brighter.commandprocessor.messaginggateway.restms.Model;
using Thinktecture.IdentityModel.Hawk.Client;

namespace paramore.brighter.commandprocessor.messaginggateway.restms
{
    internal class Pipe
    {
        readonly RestMSMessageGateway gateway;
        readonly Join join;

        public string PipeUri { get; private set; }

        public Pipe(RestMsMessageConsumer gateway, Feed feed)
        {
            this.gateway = gateway;
            join = new Join(gateway, feed);
        }


        public void EnsurePipeExists(string pipeTitle, string routingKey, RestMSDomain domain, ClientOptions options, double timeout)
        {
            gateway.Logger.DebugFormat("Checking for existence of the pipe {0} on the RestMS server: {1}", pipeTitle, gateway.Configuration.RestMS.Uri.AbsoluteUri);
            var pipeExists = PipeExists(pipeTitle, domain);
            if (!pipeExists)
            {
                domain = CreatePipe(domain.Href, pipeTitle, options, timeout);
                if (domain == null || !domain.Pipes.Any(dp => dp.Title == pipeTitle))
                {
                    throw new RestMSClientException(string.Format("Unable to create pipe {0} on the default domain; see log for errors", pipeTitle));
                }

                join.CreateJoin(domain.Pipes.First(p => p.Title == pipeTitle).Href, routingKey, options, timeout);
            }

            PipeUri = domain.Pipes.First(dp => dp.Title == pipeTitle).Href;
        }

        public RestMSPipe GetPipe(ClientOptions options, double timeout)
        {
            /*TODO: Optimize this by using a repository approach with the repository checking for modification 
            through etag and serving existing version if not modified and grabbing new version if changed*/

            gateway.Logger.DebugFormat("Getting the pipe from the RestMS server: {0}", PipeUri);
            var client = gateway.CreateClient(options, timeout);

            try
            {
                var response = client.GetAsync(PipeUri).Result;
                response.EnsureSuccessStatusCode();
                return gateway.ParseResponse<RestMSPipe>(response);
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    gateway.Logger.ErrorFormat("Threw exception getting Pipe {0} from RestMS Server {1}", PipeUri, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error retrieving the domain from the RestMS server, see log for details"));
            }

        }

        RestMSDomain CreatePipe(string domainUri, string title, ClientOptions options, double timeout)
        {
            gateway.Logger.DebugFormat("Creating the pipe {0} on the RestMS server: {1}", title, gateway.Configuration.RestMS.Uri.AbsoluteUri);
            var client = gateway.CreateClient(options, timeout);
            try
            {
                var response = client.SendAsync(gateway.CreateRequest(
                    domainUri, gateway.CreateEntityBody(
                        new RestMSPipeNew
                        {
                            Type = "Default",
                            Title = title
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
                    gateway.Logger.ErrorFormat("Threw exception adding Pipe {0} to RestMS Server {1}", title, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error adding the Feed {0} to the RestMS server, see log for details", title));
            }
        }

        bool PipeExists(string pipeTitle, RestMSDomain domain)
        {
            return domain != null && domain.Pipes != null && domain.Pipes.Any(p => p.Title == pipeTitle);
        }
    }
}