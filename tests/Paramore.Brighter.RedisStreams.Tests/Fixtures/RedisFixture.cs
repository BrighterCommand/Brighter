using System;
using Paramore.Brighter.MessagingGateway.RedisStreams;

namespace Paramore.Brighter.RedisStreams.Tests.Fixtures
{
    public class RedisFixture : IDisposable
    {
        public RedisStreamsConfiguration Configuration => new RedisStreamsConfiguration { ConfigurationOptions = "localhost:6379", };
        public readonly string QueueName = "Brighter_Test_Stream";
        public IAmAMessageProducer Producer => new RedisStreamsProducer(Configuration);
        public IAmAMessageConsumer Consumer => new RedisStreamsConsumer(Configuration, QueueName);

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~RedisFixture()
        {
            ReleaseUnmanagedResources();
        }
        
        private void ReleaseUnmanagedResources()
        {
            Producer.Dispose();
        }

     }
}
