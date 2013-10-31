using Machine.Specifications;
using TinyIoC;
using paramore.commandprocessor.ioccontainers.IoCContainers;

namespace paramore.commandprocessor.tests.IoCContainers
{
    [Subject(typeof(TinyIoCAdapter))]
    public class When_creating_a_wrapped_TinyIoCInstance
    {
       static TinyIoCAdapter _container;

        Establish context = () => _container = new TinyIoCAdapter(new TinyIoCContainer());

        It should_create_a_wrapped_IoC_container = () => _container.ShouldBeOfType<IAdaptAnInversionOfControlContainer>();
    }

    public class When_resolving_an_interface_implementation
    {
        static TinyIoCAdapter _container;

        Establish context = () => _container = new TinyIoCAdapter(new TinyIoCContainer());

        Because of = () => _container.Register<IMyInterface, MyInterfaceImpl>();

        It should_resolve_instances_of_the_interface = () => _container.Resolve(typeof(IMyInterface)).ShouldBeOfType<IMyInterface>();
    }

    public class When_resolving_an_interface_implementation_using_generic_shorthand
    {
        static TinyIoCAdapter _container;

        Establish context = () => _container = new TinyIoCAdapter(new TinyIoCContainer());

        Because of = () => _container.Register<IMyInterface, MyInterfaceImpl>();

        It should_resolve_instances_of_the_interface = () => _container.Resolve<IMyInterface>().ShouldBeOfType<IMyInterface>();
    }

    public class When_resolving_an_interface_implementation_should_allow_disambiguation_by_name
    {
        static TinyIoCAdapter _container;

        Establish context = () =>
        {
            _container = new TinyIoCAdapter(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>("FirstImpl");
            _container.Register<IMyInterface, MyOtherIntefaceImpl>("SecondImpl");
        };

        It should_resolve_instances_of_the_interface = () => _container.Resolve(typeof(IMyInterface), "FirstImpl").ShouldBeOfType<MyInterfaceImpl>();
    }

    public class When_resolving_an_interface_implementation_using_generics_should_allow_disambiguation_by_name
    {
        static TinyIoCAdapter _container;

        Establish context = () =>
        {
            _container = new TinyIoCAdapter(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>("FirstImpl");
            _container.Register<IMyInterface, MyOtherIntefaceImpl>("SecondImpl");
        };

        It should_resolve_instances_of_the_interface = () => _container.Resolve<IMyInterface>("FirstImpl").ShouldBeOfType<MyInterfaceImpl>();
    }
    public class When_resolving_an_interface_implementation_should_support_singleton
    {
        static TinyIoCAdapter _container;

        Establish context = () =>
        {
            _container = new TinyIoCAdapter(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>().AsSingleton();
        };

        It should_resolve_instances_of_the_interface = () => _container.Resolve(typeof(IMyInterface)).ShouldBeTheSameAs(_container.Resolve(typeof(IMyInterface)));
    }

    public class When_resolving_an_interface_implementation_should_support_multiple_implementations
    {
        static TinyIoCAdapter _container;

        Establish context = () =>
        {
            _container = new TinyIoCAdapter(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>().AsMultiInstance();
        };
        
        It should_resolve_instances_of_the_interface = () => _container.Resolve(typeof(IMyInterface)).ShouldNotBeTheSameAs(_container.Resolve(typeof(IMyInterface)));
    }

    internal class MyInterfaceImpl : IMyInterface {}
    internal class MyOtherIntefaceImpl : IMyInterface {}
    internal interface IMyInterface { }
}
