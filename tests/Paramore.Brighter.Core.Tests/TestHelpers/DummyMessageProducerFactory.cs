using System.Collections.Generic;

namespace Paramore.Brighter.Core.Tests.TestHelpers
{
    public class DummyMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly IEnumerable<Publication> _publications;

        public DummyMessageProducerFactory(IEnumerable<Publication> publications)
        {
            _publications = publications;
        }

        public Dictionary<string, IAmAMessageProducer> Create()
        {
            var producers = new Dictionary<string, IAmAMessageProducer>();

            foreach (var publication in _publications)
            {
                producers.Add(publication.Topic, new DummyMessageProducer(publication.Topic));
            }

            return producers;
        }
    }
}
