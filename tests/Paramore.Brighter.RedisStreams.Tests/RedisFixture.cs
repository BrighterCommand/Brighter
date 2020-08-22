using System;
using Paramore.Brighter.MessagingGateway.RedisStreams;

namespace Paramore.Brighter.RedisStreams.Tests
{
    public class RedisFixture : IDisposable
    {
        public RedisStreamsConfiguration Configuration => new RedisStreamsConfiguration { ConfigurationOptions = "localhost:6379", };
        public IAmAMessageProducer Producer => new RedisStreamsProducer(Configuration);

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
