using Paramore.Brighter;

namespace OpenTelemetry.Shared.Commands;

public class UpdateProductCommand : Command
{
    public UpdateProductCommand(string name): base(Guid.NewGuid())
    {
        Name = name;
    }

    public string Name { get; }
}
