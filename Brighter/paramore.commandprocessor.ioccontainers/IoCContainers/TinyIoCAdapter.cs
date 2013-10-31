using System;
using System.Collections.Generic;
using System.Diagnostics;
using TinyIoC;

namespace paramore.commandprocessor.ioccontainers.IoCContainers
{
    public class TinyIoCAdapter : IAdaptAnInversionOfControlContainer
    {
        private readonly TinyIoCContainer _container;
        private TinyIoCContainer.RegisterOptions _registerOptions;

        public TinyIoCAdapter(TinyIoCContainer container)
        {
            _container = container;
        }

        public IEnumerable<object> ResolveAll(Type resolveType, bool includeUnamed)
        {
            return _container.ResolveAll(resolveType, includeUnamed);
        }

        public object Resolve(Type resolveType)
        {
            return _container.Resolve(resolveType);
        }

        public object Resolve(Type resolveType, string name)
        {
            return _container.Resolve(resolveType, name);
        }

        public ResolveType Resolve<ResolveType>() where ResolveType : class
        {
            return _container.Resolve<ResolveType>();
        }

        public ResolveType Resolve<ResolveType>(string name) where ResolveType : class
        {
            return _container.Resolve<ResolveType>(name);
        }

        public IAdaptAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>() where RegisterType : class where RegisterImplementation : class, RegisterType
        {
            _registerOptions = _container.Register<RegisterType, RegisterImplementation>();
            return this;
        }

        public IAdaptAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>(string name) where RegisterType : class where RegisterImplementation : class, RegisterType
        {
            _registerOptions = _container.Register<RegisterType, RegisterImplementation>(name);
            return this;
        }

        public IAdaptAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>(RegisterImplementation instance) where RegisterType : class where RegisterImplementation : class, RegisterType
        {
            _registerOptions = _container.Register<RegisterType, RegisterImplementation>(instance);
            return this;
        }

        public void AsMultiInstance()
        {
            Debug.Assert(_registerOptions != null);
            _registerOptions.AsMultiInstance();
        }

        public void AsSingleton()
        {
            Debug.Assert(_registerOptions != null);
            _registerOptions.AsSingleton();
        }
    }
}
