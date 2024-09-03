namespace Paramore.Brighter.ServiceActivator.Status
{
    public class PerformerInformation (string name, ConsumerState state)
    {
        public string Name { get; } = name;
        public ConsumerState State { get; } = state;
    }
}
