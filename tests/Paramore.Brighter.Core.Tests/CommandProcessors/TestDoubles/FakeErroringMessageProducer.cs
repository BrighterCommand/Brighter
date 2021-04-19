using System;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    public class FakeErroringMessageProducer : IAmAMessageProducer
    {
        public int MaxOutStandingMessages { get; set; } = -1;
        public int MaxOutStandingCheckIntervalMilliSeconds { get; set; } = 0;
        public int SentCalledCount { get; set; }
        public void Dispose() { }

        public void Send(Message message)
        {
            SentCalledCount++;
            throw new Exception();
        }
        
        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            Send(message);
        }
  
    }
}
