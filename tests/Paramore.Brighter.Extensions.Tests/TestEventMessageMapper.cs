using Paramore.Brighter;

namespace Tests
{
    public class TestEventMessageMapper : IAmAMessageMapper<TestEvent>
    {
        public IRequestContext Context { get; set; }

        public Message MapToMessage(TestEvent request, Publication publication)  
        {
            throw new System.NotImplementedException();
        }

        public TestEvent MapToRequest(Message message)
        {
            throw new System.NotImplementedException();
        }
    }
}
