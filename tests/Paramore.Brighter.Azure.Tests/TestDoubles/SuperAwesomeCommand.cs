namespace Paramore.Brighter.Azure.Tests.TestDoubles;

public class SuperAwesomeCommand : Command
{
    public string Message { get; set; }

    public SuperAwesomeCommand() : base(Guid.NewGuid())
    {
    }
}
