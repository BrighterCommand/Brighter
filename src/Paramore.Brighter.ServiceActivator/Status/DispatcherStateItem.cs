namespace Paramore.Brighter.ServiceActivator.Status
{
    public class DispatcherStateItem(string name, int expectedPerformners, params PerformerInformation[] performers)
    {
        public string Name { get; } = name;
        public PerformerInformation[] Performers { get; } = performers;
        public int ExpectPerformers { get; } = expectedPerformners;
    }
}
