namespace Paramore.Brighter.InMemory.Tests.TestDoubles
{
    public static class MessageExtensions
    {
        public static IRequest ToStubRequest(this Message message)
        {
            return new Command(message.Id);
        }
    }
}
