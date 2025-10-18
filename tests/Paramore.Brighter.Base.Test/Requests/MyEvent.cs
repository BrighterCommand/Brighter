namespace Paramore.Brighter.Base.Test.Requests;

public class MyEvent() : Event(Id.Random())
{
    public string Value { get; set; } = string.Empty;
}
