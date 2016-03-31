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
using System.Collections.Generic;
using System.Linq;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator.ServiceActivatorConfiguration
{
    internal class ConnectionFactory
    {
        private readonly IAmAChannelFactory _channelFactory;

        public ConnectionFactory(IAmAChannelFactory channelFactory)
        {
            _channelFactory = channelFactory;
        }

        public IEnumerable<Connection> Create(IEnumerable<ConnectionElement> connectionElements)
        {
            var connections =
            (
               from ConnectionElement connectionElement in connectionElements
               select new Connection(
                   name: new ConnectionName(connectionElement.ConnectionName),
                   channelFactory: _channelFactory,
                   dataType: GetType(connectionElement.DataType),
                   noOfPerformers: connectionElement.NoOfPerformers,
                   timeoutInMilliseconds: connectionElement.TimeoutInMiliseconds,
                   requeueCount: connectionElement.RequeueCount,
                   requeueDelayInMilliseconds: connectionElement.RequeueDelayInMilliseconds,
                   unacceptableMessageLimit: connectionElement.UnacceptableMessageLimit,
                   channelName: new ChannelName(connectionElement.ChannelName),
                   routingKey: connectionElement.RoutingKey,
                   isDurable: connectionElement.IsDurable,
                   isAsync:connectionElement.IsAsync
                   )
             ).ToList();


            return connections;
        }

        public static Type GetType(string typeName)
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var doesAssemblyHaveAMatchingType = assemblies.Select(a => a.GetType(typeName));
                var matchingTypes = doesAssemblyHaveAMatchingType.Where(type => type != null).Select(type => type);
                var match = matchingTypes.First();
                return match;
            }
            catch (Exception e)
            {
                throw new ConfigurationException("Data Type for Connection cannot be found in the component", e);
            }
  
        }
    }
}