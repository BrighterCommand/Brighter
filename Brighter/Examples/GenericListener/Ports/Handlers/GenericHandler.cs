using GenericListener.Ports.Events;
using GenericListener.Ports.Indexers;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;

namespace GenericListener.Ports.Handlers
{
    public class GenericHandler<T> : RequestHandler<T> where T : EventStoredEvent
    {
        private readonly IGenericFeedEventIndexer<T> _indexer;

        public GenericHandler(IGenericFeedEventIndexer<T> indexer)
        {
            _indexer = indexer;
        }

        public override T Handle(T command)
        {
            logger.InfoFormat("Received {1} {0}", command.Id, typeof(T).FullName);

            _indexer.Index(command);

            return base.Handle(command);
        }
    }
}