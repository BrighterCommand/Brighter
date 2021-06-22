using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class ScopeCache
    {
        private readonly IServiceProvider _serviceProvider;
        private const string _scopeIdentifier = "scopeIdentifier";
        private readonly ConcurrentDictionary<Guid, IServiceScope> _scopeCache =
            new ConcurrentDictionary<Guid, IServiceScope>();

        public ScopeCache(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IServiceScope GetOrCreateScope(out Guid scopeId)
        {
            var scope = _serviceProvider.CreateScope();
            scopeId = Guid.NewGuid();
            _scopeCache.AddOrUpdate(scopeId, scope, (guid, serviceScope) => serviceScope);
            return scope;
        }

        public IServiceScope Get(Guid scopeId)
        {
            if (!_scopeCache.TryGetValue(scopeId, out var scope))
            {
                //err?
            }
            
            return scope;
        }

        public void SetIdentifierOnHandlerContext(IRequestContext handleRequestsContext, Guid scopeId)
        {
            handleRequestsContext.Bag.Add(_scopeIdentifier, scopeId);
        }

        public static Guid GetIdentifierFromHandlerContext(IRequestContext handleRequestsContext)
        {
            return (Guid)handleRequestsContext.Bag[_scopeIdentifier];
        }

        public void RemoveScopeFromCache(IRequestContext handlerContext)
        {
            if (handlerContext != null
                && handlerContext.Bag.TryGetValue(_scopeIdentifier, out var scopeId))
            {
                RemoveScopeFromCache((Guid)scopeId);
            }
        }

        public void RemoveScopeFromCache(Guid scopeId)
        {
            _scopeCache[scopeId]?.Dispose();
        }
    }
}
