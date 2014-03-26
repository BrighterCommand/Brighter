using System;
using System.Linq;
using paramore.brighter.commandprocessor.extensions;

namespace paramore.brighter.commandprocessor
{
    public class CommandProcessor : IAmACommandProcessor
    {
        private readonly IAdaptAnInversionOfControlContainer container;
        private readonly IAmARequestContextFactory requestContextFactory;
        private IAmAMessageStore<Message> messageStore;
        private IAmAMessagingGateway messsagingGateway;

        public CommandProcessor(IAdaptAnInversionOfControlContainer container, IAmARequestContextFactory requestContextFactory)
        {
            this.container = container;
            this.requestContextFactory = requestContextFactory;
        }

        public CommandProcessor(IAdaptAnInversionOfControlContainer container, IAmARequestContextFactory requestContextFactory, IAmAMessageStore<Message> messageStore, IAmAMessagingGateway messsagingGateway)
            :this(container, requestContextFactory)
        {
            this.messageStore = messageStore;
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
            var messageMapper = container.GetInstance<IAmAMessageMapper<T, Message>>();
            var message = messageMapper.Map(command);
            messageStore.Add(message);
            messsagingGateway.SendMessage(message);
        }

        public void Repost(Guid messageId)
        {
            var message = messageStore.Get(messageId);
            messsagingGateway.SendMessage(message);
        }
    }
}