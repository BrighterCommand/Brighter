namespace Paramore.Brighter.AWSScheduler.Tests.TestDoubles
{
    internal sealed class MyDeferredCommand() : Command(Guid.NewGuid())
    {
        public string Value { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
    }
}
