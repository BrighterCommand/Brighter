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
using paramore.brighter.commandprocessor.extensions;

namespace paramore.brighter.commandprocessor
{
    public class CommandProcessor : IAmACommandProcessor
    {
        private readonly IAdaptAnInversionOfControlContainer container;
        private readonly IAmARequestContextFactory requestContextFactory;
        private IAmAMessageStore<CommandMessage> commandRepository;
        private IAmAMessagingGateway messsagingGateway;

        public CommandProcessor(IAdaptAnInversionOfControlContainer container, IAmARequestContextFactory requestContextFactory)
        {
            this.container = container;
            this.requestContextFactory = requestContextFactory;
        }

        public CommandProcessor(IAdaptAnInversionOfControlContainer container, IAmARequestContextFactory requestContextFactory, IAmAMessageStore<CommandMessage> commandRepository, IAmAMessagingGateway messsagingGateway)
            :this(container, requestContextFactory)
        {
            this.commandRepository = commandRepository;
            this.messsagingGateway = messsagingGateway;
        }

        public void Send<T>(T command) where T : class, IRequest
        {
            using (var builder = new PipelineBuilder<T>(container))
            {
                var requestContext = requestContextFactory.Create(container);
                var handlerChain = builder.Build(requestContext);

                var handlerCount = handlerChain.Count();

                if (handlerCount > 1)
                    throw new ArgumentException(string.Format("More than one handler was found for the typeof command {0} - a command should only have one handler.", typeof (T)));
                if (handlerCount == 0)
                    throw new ArgumentException(string.Format("No command handler was found for the typeof command {0} - a command should have only one handler.",typeof (T)));

                handlerChain.First().Handle(command);
            }
        }

        public void Publish<T>(T @event) where T : class, IRequest
        {
            using (var builder = new PipelineBuilder<T>(container))
            {
                var requestContext = new RequestContext(container);
                var handlerChain = builder.Build(requestContext);

                handlerChain.Each(chain => chain.Handle(@event));
            }
        }

        public void Post<T>(T command) where T : class, IRequest
        {
        }

        public void Repost(Guid messageId)
        {
        }
    }
}