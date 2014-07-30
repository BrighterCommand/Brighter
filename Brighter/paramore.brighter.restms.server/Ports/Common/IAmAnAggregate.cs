namespace paramore.brighter.restms.server.Ports.Common
{
    internal interface IAmAnAggregate
    {
        Identity Id { get; }
        AggregateVersion Version { get; }
    }
}