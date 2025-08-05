namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    internal sealed class MyCommandToFail : ICommand
    {
        public Id? CorrelationId { get; set; }
        public Id Id { get; set; } = Id.Random();
    }
}
