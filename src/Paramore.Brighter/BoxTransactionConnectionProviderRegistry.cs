using System.Collections.Generic;

namespace Paramore.Brighter
{
    public class BoxTransactionConnectionProviderRegistry : IAmABoxTransactionConnectionProviderRegistry
    {
        private readonly Dictionary<string, IAmABoxTransactionConnectionProvider> _providers;
        private string _defaultProvider;

        public BoxTransactionConnectionProviderRegistry(string providerName, IAmABoxTransactionConnectionProvider provider)
        {
            _providers = new Dictionary<string, IAmABoxTransactionConnectionProvider>();
            
            _providers.Add(providerName, provider);
            _defaultProvider = providerName;
        }

        public IAmABoxTransactionConnectionProviderRegistry AddProvider(string name, IAmABoxTransactionConnectionProvider provider, bool isDefault = false)
        {
            _providers.Add(name, provider);
            if (isDefault) _defaultProvider = name;
            return this;
        }
        
        public IAmABoxTransactionConnectionProvider GetDefault()
        {
            return _providers[_defaultProvider];
        }

        public IAmABoxTransactionConnectionProvider Lookup(string name)
        {
            return _providers[name];
        }
    }
}
