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

using Common.Logging;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator
{
    internal class ConsumerFactory<TRequest> : IConsumerFactory where TRequest : class, IRequest
    {
        private readonly IAmACommandProcessor commandProcessor;
        private readonly IAmAMessageMapperRegistry messageMapperRegistry;
        private readonly Connection connection;
        private readonly ILog logger;

        public ConsumerFactory(IAmACommandProcessor commandProcessor, IAmAMessageMapperRegistry messageMapperRegistry, Connection connection, ILog logger)
        {
            this.commandProcessor = commandProcessor;
            this.messageMapperRegistry = messageMapperRegistry;
            this.connection = connection;
            this.logger = logger;
        }

        public Consumer Create()
        {
            var messagePump = new MessagePump<TRequest>(commandProcessor, messageMapperRegistry.Get<TRequest>())
            {
                Channel = connection.Channel,
                TimeoutInMilliseconds = connection.TimeoutInMiliseconds,
                RequeueCount = connection.RequeueCount,
                Logger = logger
            };
            var consumer = new Consumer(connection.Name, connection.Channel, messagePump);
            return consumer;
        }
    }
}