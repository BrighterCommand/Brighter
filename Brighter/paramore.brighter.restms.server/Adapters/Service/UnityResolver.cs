using System;
using System.Collections.Generic;
using System.Web.Http.Dependencies;
using Microsoft.Practices.Unity;

namespace paramore.brighter.restms.server.Adapters.Service
{
public class UnityResolver : IDependencyResolver
{
    readonly IUnityContainer container;
    bool disposed;

    public UnityResolver(IUnityContainer container)
    {
        this.container = container;
    }

    public object GetService(Type serviceType)
    {
        try
        {
            return container.Resolve(serviceType);
        }
        catch (ResolutionFailedException)
        {
            return null;
        }
    }

    public IEnumerable<object> GetServices(Type serviceType)
    {
        try
        {
            return container.ResolveAll(serviceType);
        }
        catch (ResolutionFailedException)
        {
            return new object[0];
        }
    }

    public IDependencyScope BeginScope()
    {
        var child = container.CreateChildContainer();
        return new UnityResolver(child);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~UnityResolver()
    {
        Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        if (disposed)
            return;

        if (disposing)
            container.Dispose();

        disposed = true;
    }
}
}
