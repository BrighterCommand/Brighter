using System;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

public class SpyContextFactory : IAmARequestContextFactory
{
    public bool CreateWasCalled { get; set; }
    public RequestContext? Context { get; set; }
    
    public RequestContext Create()
    {
        CreateWasCalled = true;
        
        Context = new RequestContext();
        var testContent = Guid.NewGuid().ToString();
        Context.Bag["TestString"] = testContent;
        return Context;
    }
}
