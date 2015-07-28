using System;
using System.Collections.Generic;
using System.Linq;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;

namespace paramore.brighter.commandprocessor.messageviewer.Ports.Handlers
{
    public class RepostCommand : ICommand
    {
        public List<string> MessageIds { get; set; }
        public string StoreName { get; set; }
    }

    public class RepostCommandHandler : IHandleCommand<RepostCommand>
    {
        private readonly IMessageStoreViewerFactory _messageStoreViewerFactory;
        private readonly IMessageProducerFactoryProvider _messageProducerFactoryProvider;
        private readonly ILog _logger = LogProvider.GetLogger("RepostCommandHandler");

        public RepostCommandHandler(IMessageStoreViewerFactory messageStoreViewerFactory,
                                    IMessageProducerFactoryProvider messageProducerFactoryProvider)
        {
            _messageStoreViewerFactory = messageStoreViewerFactory;
            _messageProducerFactoryProvider = messageProducerFactoryProvider;
        }

        /// <exception cref="SystemException">Store not found / Mis-configured viewer broker</exception>
        public void Handle(RepostCommand command)
        {
            CheckMessageIds(command);            
            var messageStore = GetMessageStore(command);
            var foundMessages = GetMessagesFromStore(command, messageStore);
            var foundProducer = GetMessageProducer(_messageProducerFactoryProvider);

            foreach (var foundMessage in foundMessages)
            {
                foundProducer.Send(foundMessage).Wait();
            }
        }

        private IAmAMessageProducer GetMessageProducer(IMessageProducerFactoryProvider messageProducerFactoryProvider)
        {
            var messageProducerFactory = messageProducerFactoryProvider.Get(_logger);
            if (messageProducerFactory == null)
            {
                throw new SystemException("Mis-configured viewer - no message producer found");
            }
            IAmAMessageProducer messageProducer = null;
            Exception foundException = null;
            try
            {
                messageProducer = messageProducerFactory.Create();
            }
            catch (Exception e)
            {
                foundException = e;
            }
            if (messageProducer == null)
            {
                string message = "Mis-configured viewer - cannot create found message producer";
                if (foundException != null)
                {
                    message += ". " + foundException.Message;
                }
                throw new SystemException(message);
            }
            return messageProducer;
        }

        private static List<Message> GetMessagesFromStore(RepostCommand command, IAmAMessageStore<Message> messageStore)
        {
            var foundMessages = new List<Message>(
                command.MessageIds
                    .Select(messageId => messageStore.Get(Guid.Parse(messageId)).Result)
                    .Where(fm => fm != null));
            if (foundMessages.Count < command.MessageIds.Count)
            {
                throw new SystemException("Cannot find messages " +
                    string.Join(",", command.MessageIds.Where(id => foundMessages.All(fm => fm.Id.ToString() != id.ToString())).ToArray()));
            }
            return foundMessages;
        }

        private IAmAMessageStore<Message> GetMessageStore(RepostCommand command)
        {
            IAmAMessageStore<Message> messageStore = _messageStoreViewerFactory.Connect(command.StoreName);
            if (messageStore == null)
            {
                throw new SystemException("Error " + RepostCommandHandlerError.StoreNotFound);
            }
            return messageStore;
        }

        private static void CheckMessageIds(RepostCommand command)
        {
            if (command.MessageIds == null)
            {
                throw new SystemException("Error null MessageIds");
            }
        }
    }

    internal enum RepostCommandHandlerError
    {
        StoreNotFound
    }
}