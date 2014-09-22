namespace paramore.brighter.restms.server.Ports.Common
{
    public interface IAmAnAggregate
    {
        Identity Id { get; }
        AggregateVersion Version { get; }
    }
}