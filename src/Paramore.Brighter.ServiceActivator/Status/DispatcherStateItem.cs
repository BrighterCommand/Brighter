namespace Paramore.Brighter.ServiceActivator.Status
{
    public class DispatcherStateItem
    {
        public string Name { get; set; }
        public PerformerInformation[] Performers { get; set; }
        public int ExpectPerformers { get; set; }
    }
}
