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
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.restms.Exceptions;
using paramore.brighter.commandprocessor.messaginggateway.restms.Model;

namespace paramore.brighter.commandprocessor.messaginggateway.restms
{
    internal class Join
    {
        private readonly RestMSMessageGateway _gateway;
        private readonly Feed _feed;

        public Join(RestMsMessageConsumer gateway, Feed feed)
        {
            _gateway = gateway;
            _feed = feed;
        }

        public RestMSJoin CreateJoin(string pipeUri, string routingKey)
        {
            _gateway.Logger.DebugFormat("Creating the join with key {0} for pipe {1}", routingKey, pipeUri);
            var client = _gateway.Client();
            try
            {
                var response = client.SendAsync(
                    _gateway.CreateRequest(
                        pipeUri,
                        _gateway.CreateEntityBody(
                            new RestMSJoin
                            {
                                Address = routingKey,
                                Feed = _feed.FeedUri,
                                Type = "Default"
                            }
                            )
                        )
                    )
                                     .Result;

                response.EnsureSuccessStatusCode();
                var pipe = _gateway.ParseResponse<RestMSPipe>(response);
                return pipe.Joins.FirstOrDefault();
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    _gateway.Logger.ErrorFormat("Threw exception adding join with routingKey {0} to Pipe {1} on RestMS Server {2}", routingKey, pipeUri, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error adding the join with routingKey {0} to Pipe {1} to the RestMS server, see log for details", routingKey, pipeUri));
            }
        }
    }
}
