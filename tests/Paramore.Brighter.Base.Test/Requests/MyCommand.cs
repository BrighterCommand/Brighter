namespace Paramore.Brighter.Base.Test.Requests;

public class MyCommand(): Command(Id.Random())
{
    public string Value { get; set; } = string.Empty;
}
