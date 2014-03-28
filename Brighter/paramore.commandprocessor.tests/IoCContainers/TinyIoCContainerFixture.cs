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
using Machine.Specifications;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.ioccontainers.Adapters;

namespace paramore.commandprocessor.tests.IoCContainers
{
    [Subject(typeof(TinyIoCAdapter))]
    public class When_creating_a_wrapped_TinyIoCInstance
    {
       static TinyIoCAdapter _container;

        Establish context = () => _container = new TinyIoCAdapter(new TinyIoCContainer());

        It should_create_a_wrapped_IoC_container = () => _container.ShouldBeAssignableTo<IAdaptAnInversionOfControlContainer>();
    }

    public class When_resolving_an_interface_implementation
    {
        static TinyIoCAdapter _container;
        static IDisposable _lifetime;

        Establish context = () => _container = new TinyIoCAdapter(new TinyIoCContainer());

        Because of = () =>
            {
                _container.Register<IMyInterface, MyInterfaceImpl>();
                _lifetime = _container.CreateLifetime();
            };

        It should_resolve_instances_of_the_interface = () => _container.GetInstance(typeof(IMyInterface)).ShouldBeAssignableTo<IMyInterface>();

        Cleanup teardown = () => _lifetime.Dispose();
    }

    public class When_resolving_an_interface_implementation_using_generic_shorthand
    {
        static TinyIoCAdapter _container;
        static IDisposable _lifetime;

        Establish context = () => _container = new TinyIoCAdapter(new TinyIoCContainer());

        Because of = () =>
            {
                _container.Register<IMyInterface, MyInterfaceImpl>();
                _lifetime = _container.CreateLifetime();
            };

        It should_resolve_instances_of_the_interface = () => _container.GetInstance<IMyInterface>().ShouldBeAssignableTo<IMyInterface>();

        Cleanup teardown = () => _lifetime.Dispose();
    }

    public class When_resolving_an_interface_implementation_should_allow_disambiguation_by_name
    {
        static TinyIoCAdapter _container;
        static IDisposable _lifetime;

        Establish context = () =>
        {
            _container = new TinyIoCAdapter(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>("FirstImpl");
            _container.Register<IMyInterface, MyOtherIntefaceImpl>("SecondImpl");
            _lifetime = _container.CreateLifetime();
        };

        It should_resolve_instances_of_the_interface = () => _container.GetInstance(typeof(IMyInterface), "FirstImpl").ShouldBeAssignableTo<MyInterfaceImpl>();

        Cleanup teardown = () => _lifetime.Dispose();
    }

    public class When_resolving_an_interface_implementation_should_allow_specific_instance
    {
        static TinyIoCAdapter _container;
        static readonly MyInterfaceImpl myInterfaceImpl = new MyInterfaceImpl();
        static IDisposable _lifetime;

        Establish context = () =>
        {
            _container = new TinyIoCAdapter(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>(myInterfaceImpl);
            _lifetime = _container.CreateLifetime();
        };

        It should_resolve_instances_of_the_interface = () => _container.GetInstance(typeof (IMyInterface)).ShouldBeTheSameAs(myInterfaceImpl);

        Cleanup teardown = () => _lifetime.Dispose();
    }

    public class When_resolving_an_interface_implementation_using_generics_should_allow_disambiguation_by_name
    {
        static TinyIoCAdapter _container;
        static IDisposable _lifetime;

        Establish context = () =>
        {
            _container = new TinyIoCAdapter(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>("FirstImpl");
            _container.Register<IMyInterface, MyOtherIntefaceImpl>("SecondImpl");
            _lifetime = _container.CreateLifetime();
        };

        It should_resolve_instances_of_the_interface = () => _container.GetInstance<IMyInterface>("FirstImpl").ShouldBeAssignableTo<MyInterfaceImpl>();

        Cleanup teardown = () => _lifetime.Dispose();
    }
    public class When_resolving_an_interface_implementation_should_support_singleton
    {
        static TinyIoCAdapter _container;
        static IDisposable _lifetime;

        Establish context = () =>
        {
            _container = new TinyIoCAdapter(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>().AsSingleton();
            _lifetime = _container.CreateLifetime();
        };

        It should_resolve_instances_of_the_interface = () => _container.GetInstance(typeof(IMyInterface)).ShouldBeTheSameAs(_container.GetInstance(typeof(IMyInterface)));

        Cleanup teardown = () => _lifetime.Dispose();
    }

    public class When_resolving_an_interface_implementation_should_support_multiple_implementations
    {
        static TinyIoCAdapter _container;
        static IDisposable _lifetime;

        Establish context = () =>
        {
            _container = new TinyIoCAdapter(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>().AsMultiInstance();
            _lifetime = _container.CreateLifetime();
        };
        
        It should_resolve_instances_of_the_interface = () => _container.GetInstance(typeof(IMyInterface)).ShouldNotBeTheSameAs(_container.GetInstance(typeof(IMyInterface)));

        Cleanup teardown = () => _lifetime.Dispose();
    }

    internal class MyInterfaceImpl : IMyInterface {}
    internal class MyOtherIntefaceImpl : IMyInterface {}
    internal interface IMyInterface { }
}
