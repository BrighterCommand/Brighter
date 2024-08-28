using System.Collections.Generic;

namespace Paramore.Brighter.Core.Tests.TestHelpers
{
    public class DummyMessageProducer : IAmAMessageProducer
    {
        public string Topic { get; }
        public int MaxOutStandingMessages { get; set; }
        public int MaxOutStandingCheckIntervalMilliSeconds { get; set; }
        public Dictionary<string, object> OutBoxBag { get; set; }

        public DummyMessageProducer(string topic)
        {
            Topic = topic;
        }

        public void Dispose()
        {
            
        }
    }
}
