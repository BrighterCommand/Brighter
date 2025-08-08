namespace Paramore.Brighter.Pulsar.Tests.TestDoubles;

internal class MyDeferredCommand : Command
{
    public string Value { get; set; }
    public MyDeferredCommand() : base(Guid.NewGuid()) { }
        
}
