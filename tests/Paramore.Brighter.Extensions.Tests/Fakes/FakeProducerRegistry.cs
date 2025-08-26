using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests.Fakes
{
    internal class FakeProducerRegistry : IAmAProducerRegistryFactory
    {
        public IAmAProducerRegistry Create()
        {
            return new ProducerRegistry(new Dictionary<ProducerKey, IAmAMessageProducer>()
            {
                {
                    new ProducerKey("greeting.event"), 
                    new FakeMessageProducer()
                }
            });
        }

        public Task<IAmAProducerRegistry> CreateAsync(CancellationToken ct = default)
        {
            return Task.FromResult(Create());
        }
    }
}
