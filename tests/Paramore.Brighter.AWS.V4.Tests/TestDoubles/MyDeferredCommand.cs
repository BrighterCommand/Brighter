using System;

namespace Paramore.Brighter.AWS.V4.Tests.TestDoubles
{
    internal sealed class MyDeferredCommand() : Command(Guid.NewGuid())
    {
        public string Value { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
    }
}
