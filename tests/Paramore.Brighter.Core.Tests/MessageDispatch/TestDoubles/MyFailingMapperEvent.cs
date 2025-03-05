using System;
using System.Diagnostics;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

internal sealed class MyFailingMapperEvent : IRequest
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    /// <value>The identifier.</value>
    public string Id { get; set; }
        
    /// <summary>
    /// Initializes a new instance of the <see cref="T:System.Object"/> class.
    /// </summary>
    public MyFailingMapperEvent()
    {
        Id = Guid.NewGuid().ToString();
    }
        
    /// <summary>
    /// Gets or sets the span that this operation live within
    /// </summary>
    public Activity Span { get; set; }
}
