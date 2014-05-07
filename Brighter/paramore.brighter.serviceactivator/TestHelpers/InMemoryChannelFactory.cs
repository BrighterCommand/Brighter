namespace paramore.brighter.serviceactivator.TestHelpers
{
    public class InMemoryChannelFactory : IAmAChannelFactory
    {
        public IAmAnInputChannel Create(string channelName)
        {
           return new InMemoryChannel(channelName); 
        }
    }
}
