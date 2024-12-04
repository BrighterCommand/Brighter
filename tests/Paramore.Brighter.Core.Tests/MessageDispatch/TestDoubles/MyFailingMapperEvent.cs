using System;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

internal class MyFailingMapperEvent : Event
{

    /// <summary>
    /// Initializes a new instance of the <see cref="T:System.Object"/> class.
    /// </summary>
    public MyFailingMapperEvent() : base(Guid.NewGuid().ToString()) { }
}
