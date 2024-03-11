namespace Paramore.Brighter.Azure.Tests.TestDoubles;

public class SuperAwesomeCommand(string message) : Command(Guid.NewGuid())
{
    public string Message { get; set; } = message;
}
