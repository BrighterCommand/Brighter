using System;
using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class ScopedServiceProviderHandlerFactory : IAmAHandlerFactory, IAmAHandlerFactoryAsync
    {
        private readonly ScopeCache _scopeCache ;

        public ScopedServiceProviderHandlerFactory(IServiceProvider serviceProvider)
        {
            _scopeCache = new ScopeCache(serviceProvider);
        }

        IHandleRequests IAmAHandlerFactory.Create(Type handlerType)
        {
            //TODO: ISSUE - we need to get the scope id for the lifetime
            var scope = _scopeCache.GetOrCreateScope(out var scopeId);

            var handleRequests = (IHandleRequests)scope.ServiceProvider.GetService(handlerType);

            // TODO: decide if we want to set here or in the LifetimeScope
            //_scopeCache.SetIdentifierOnHandlerContext(handleRequests.Context, scopeId);
            return handleRequests;
        }

        IHandleRequestsAsync IAmAHandlerFactoryAsync.Create(Type handlerType)
        {
            //TODO: ISSUE - we need to get the scope id for the lifetime
            var scope = _scopeCache.GetOrCreateScope(out var scopeId);

            var handleRequests = (IHandleRequestsAsync)scope.ServiceProvider.GetService(handlerType);

            // TODO: decide if we want to set here or in the LifetimeScope
            //_scopeCache.SetIdentifierOnHandlerContext(handleRequests.Context, scopeId);
            return handleRequests;
        }

        public void Release(IHandleRequests handler)
        {
            _scopeCache.RemoveScopeFromCache(handler?.Context);
            var disposal = handler as IDisposable;
            disposal?.Dispose();
        }

        public void Release(IHandleRequestsAsync handler)
        {
            _scopeCache.RemoveScopeFromCache(handler?.Context);
            var disposal = handler as IDisposable;
            disposal?.Dispose();
        }

        public bool TryReleaseScope(IEnumerable<IHandleRequestsAsync> handleRequestsAsync)
        {
            return ReleaseScope(handleRequestsAsync.Select(to => to.Context));
        }

        public bool TryReleaseScope(IEnumerable<IHandleRequests> handleRequests)
        {
            return ReleaseScope(handleRequests.Select(to => to.Context));
        }

        private bool ReleaseScope(IEnumerable<IRequestContext> contexts)
        {
            var scopeIds = contexts
                .Select(ScopeCache.GetIdentifierFromHandlerContext)
                .Distinct();

            //scope Id >1 err
            _scopeCache.RemoveScopeFromCache(scopeIds.First());
            return true;
        }

        public bool TryCreateScope(IAmALifetime instanceScope)
        {
            //TODO: sort the id here
            _scopeCache.GetOrCreateScope(out var id);
            instanceScope.SetScopeId(id);
            return true;
        }
    }
}
