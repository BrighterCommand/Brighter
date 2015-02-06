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
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.restms.Exceptions;
using paramore.brighter.commandprocessor.messaginggateway.restms.Model;

namespace paramore.brighter.commandprocessor.messaginggateway.restms
{
    internal class Domain
    {
        readonly RestMSMessageGateway gateway;

        public Domain(RestMSMessageGateway gateway)
        {
            this.gateway = gateway;
        }

        /// <summary>
        /// Gets the default domain.
        /// </summary>
        /// <param name="client"></param>
        /// <returns>RestMSDomain.</returns>
        /// <exception cref="RestMSClientException"></exception>
        public RestMSDomain GetDomain()
        {
            gateway.Logger.DebugFormat("Getting the default domain from the RestMS server: {0}", gateway.Configuration.RestMS.Uri.AbsoluteUri);

            try
            {
                var response = gateway.Client().GetAsync(gateway.Configuration.RestMS.Uri).Result;
                response.EnsureSuccessStatusCode();
                return gateway.ParseResponse<RestMSDomain>(response);
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    gateway.Logger.ErrorFormat("Threw exception getting Domain from RestMS Server {0}", exception.Message);
                }

                throw new RestMSClientException(string.Format("Error retrieving the domain from the RestMS server, see log for details"));
            }
        }
    }
}