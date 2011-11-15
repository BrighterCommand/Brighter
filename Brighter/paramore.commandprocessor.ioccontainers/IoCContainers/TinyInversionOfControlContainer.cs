using System;
using System.Collections.Generic;
using System.Diagnostics;
using TinyIoC;
using paramore.commandprocessor.sharedinterfaces;

namespace paramore.commandprocessor.ioccontainers.IoCContainers
{
    public class TinyInversionOfControlContainer : IAmAnInversionOfControlContainer
    {
        private readonly TinyIoCContainer _container;
        private TinyIoCContainer.RegisterOptions _registerOptions;

        public TinyInversionOfControlContainer(TinyIoCContainer container)
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

        public IAmAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>() where RegisterType : class where RegisterImplementation : class, RegisterType
        {
            _registerOptions = _container.Register<RegisterType, RegisterImplementation>();
            return this;
        }

        public IAmAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>(string name) where RegisterType : class where RegisterImplementation : class, RegisterType
        {
            _registerOptions = _container.Register<RegisterType, RegisterImplementation>(name);
            return this;
        }

        public void AsMultiInstance()
        {
            Debug.Assert(_registerOptions != null);
            _registerOptions.AsMultiInstance();
        }
    }
}
