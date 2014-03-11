using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Practices.ServiceLocation;
using TinyIoC;

namespace paramore.commandprocessor.ioccontainers.IoCContainers
{
    public class TinyIoCAdapter : ServiceLocatorImplBase, IAdaptAnInversionOfControlContainer
    {
        private readonly TinyIoCContainer _container;
        private TinyIoCContainer.RegisterOptions _registerOptions;

        public TinyIoCAdapter(TinyIoCContainer container)
        {
            _container = container;
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

        public IAdaptAnInversionOfControlContainer Register<RegisterImplementation>(string name, RegisterImplementation instance)  where RegisterImplementation : class 
        {
            _registerOptions = _container.Register<RegisterImplementation, RegisterImplementation>(instance, name);
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

        protected override object DoGetInstance(Type serviceType, string key)
        {
            return key != null ? _container.Resolve(serviceType, key) : _container.Resolve(serviceType);
        }

        protected override IEnumerable<object> DoGetAllInstances(Type serviceType)
        {
            return _container.ResolveAll(serviceType, true);
        }
    }
}
