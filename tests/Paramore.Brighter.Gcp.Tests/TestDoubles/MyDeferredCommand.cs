namespace Paramore.Brighter.Gcp.Tests.TestDoubles;

internal sealed class MyDeferredCommand() : Command(Id.Random())
{
    public string Value { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
}
