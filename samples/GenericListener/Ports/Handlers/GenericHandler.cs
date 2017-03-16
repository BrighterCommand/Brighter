using GenericListener.Ports.Events;
using GenericListener.Ports.Indexers;
using log4net;
using Paramore.Brighter;

namespace GenericListener.Ports.Handlers
{
    public class GenericHandler<T> : RequestHandler<T> where T : EventStoredEvent
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(GenericHandler<T>));

        private readonly IGenericFeedEventIndexer<T> _indexer;

        public GenericHandler(IGenericFeedEventIndexer<T> indexer)
        {
            _indexer = indexer;
        }

        public override T Handle(T command)
        {
            _logger.InfoFormat("Received {1} {0}", command.Id, typeof(T).FullName);

            _indexer.Index(command);

            return base.Handle(command);
        }
    }
}