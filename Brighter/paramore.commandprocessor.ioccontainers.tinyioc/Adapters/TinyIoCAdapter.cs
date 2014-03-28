#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using TinyIoC;

namespace paramore.brighter.commandprocessor.ioccontainers.Adapters
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
