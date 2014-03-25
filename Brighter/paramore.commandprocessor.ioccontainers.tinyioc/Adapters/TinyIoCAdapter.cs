using System;
using System.Collections.Generic;
using System.Diagnostics;
using TinyIoC;

namespace paramore.brighter.commandprocessor.ioccontainers.IoCContainers
{
    public class TinyIoCAdapter : TrackedServiceLocator, IAdaptAnInversionOfControlContainer
    {
        private readonly TinyIoCContainer _container;
        private TinyIoCContainer.RegisterOptions _registerOptions;
        private Type _currentRegisterImplementationType;

        public TinyIoCAdapter(TinyIoCContainer container)
        {
            _container = container;
        }

        public IAdaptAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>() where RegisterType : class where RegisterImplementation : class, RegisterType
        {
            _registerOptions = _container.Register<RegisterType, RegisterImplementation>();
            _currentRegisterImplementationType = typeof(RegisterImplementation);
            return this;
        }

        public IAdaptAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>(string name) where RegisterType : class where RegisterImplementation : class, RegisterType
        {
            _registerOptions = _container.Register<RegisterType, RegisterImplementation>(name);
            _currentRegisterImplementationType = typeof(RegisterImplementation);
            return this;
        }

        public IAdaptAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>(RegisterImplementation instance) where RegisterType : class where RegisterImplementation : class, RegisterType
        {
            _registerOptions = _container.Register<RegisterType, RegisterImplementation>(instance);
            _currentRegisterImplementationType = typeof(RegisterImplementation);
            return this;
        }

        public IAdaptAnInversionOfControlContainer Register<RegisterImplementation>(string name, RegisterImplementation instance)  where RegisterImplementation : class 
        {
            _registerOptions = _container.Register<RegisterImplementation, RegisterImplementation>(instance, name);
            _currentRegisterImplementationType = typeof(RegisterImplementation);
            return this;
        }

        public IAdaptAnInversionOfControlContainer  AsMultiInstance()
        {
            Debug.Assert(_registerOptions != null);
            _registerOptions.AsMultiInstance();
            _currentRegisterImplementationType = null;
            return this;
        }

        //We choose not to track singletons, assuming that you want them across pipeline instances because they are expensive to recreate
        //The IoC container should manage the lifetime of a singleton
        public IAdaptAnInversionOfControlContainer  AsSingleton()
        {
            Debug.Assert(_registerOptions != null);
            base.DoNotTrack(_currentRegisterImplementationType);
            _registerOptions.AsSingleton();
            _currentRegisterImplementationType = null;
            return this;
        }

        protected override object DoGetInstance(Type serviceType, string key)
        {
            var instance = key != null ? _container.Resolve(serviceType, key) : _container.Resolve(serviceType);
            base.TrackItem(instance);
            return instance ;
        }

        protected override IEnumerable<object> DoGetAllInstances(Type serviceType)
        {
            var allInstances = _container.ResolveAll(serviceType, true);
            base.TrackItems(allInstances);
            return allInstances;
        }

        public void Dispose()
        {
           _container.Dispose(); 
        }
    }
}
