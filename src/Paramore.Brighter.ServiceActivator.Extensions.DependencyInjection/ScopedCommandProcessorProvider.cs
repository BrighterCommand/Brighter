using System;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public class ScopedCommandProcessorProvider : IAmACommandProcessorProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private IServiceScope _scope;

        public ScopedCommandProcessorProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        public IAmACommandProcessor Get()
        {
            if (_scope != null)
                return (IAmACommandProcessor)_scope.ServiceProvider.GetService(typeof(IAmACommandProcessor));
            return (IAmACommandProcessor)_serviceProvider.GetService(typeof(IAmACommandProcessor));
        }

        public void CreateScope()
        {
            _scope = _serviceProvider.CreateScope();
        }

        public void ReleaseScope() => Dispose();

        public void Dispose()
        {
            _scope?.Dispose();
        }
    }
}
