using System;
using System.Diagnostics;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

internal sealed class MyFailingMapperEvent : IRequest
{
    public Id? CorrelationId { get; set; }

    public Id Id { get; set; } = Id.Random();
        
}
