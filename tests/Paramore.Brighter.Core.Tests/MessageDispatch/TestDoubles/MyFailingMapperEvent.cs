using System;
using System.Diagnostics;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

internal class MyFailingMapperEvent : IRequest
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    /// <value>The identifier.</value>
    public Guid Id { get; set; }
    public string MissingStringField { get; set; }
    public int MissingIntField { get; set; }
        
    /// <summary>
    /// Initializes a new instance of the <see cref="T:System.Object"/> class.
    /// </summary>
    public MyFailingMapperEvent()
    {
        Id = new Guid();
    }
        
    /// <summary>
    /// Gets or sets the span that this operation live within
    /// </summary>
    public Activity Span { get; set; }
}
