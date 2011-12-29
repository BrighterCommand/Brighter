using Machine.Specifications;
using TinyIoC;
using paramore.commandprocessor.ioccontainers.IoCContainers;

namespace paramore.commandprocessor.tests.IoCContainers
{
    [Subject(typeof(TinyInversionOfControlContainer))]
    public class When_creating_a_wrapped_TinyIoCInstance
    {
       static TinyInversionOfControlContainer _container;

        Establish context = () => _container = new TinyInversionOfControlContainer(new TinyIoCContainer());

        It should_create_a_wrapped_IoC_container = () => _container.ShouldBeOfType<IAmAnInversionOfControlContainer>();
    }

    public class When_resolving_an_interface_implementation
    {
        static TinyInversionOfControlContainer _container;

        Establish context = () => _container = new TinyInversionOfControlContainer(new TinyIoCContainer());

        Because of = () => _container.Register<IMyInterface, MyInterfaceImpl>();

        It should_resolve_instances_of_the_interface = () => _container.Resolve(typeof(IMyInterface)).ShouldBeOfType<IMyInterface>();
    }

    public class When_resolving_an_interface_implementation_using_generic_shorthand
    {
        static TinyInversionOfControlContainer _container;

        Establish context = () => _container = new TinyInversionOfControlContainer(new TinyIoCContainer());

        Because of = () => _container.Register<IMyInterface, MyInterfaceImpl>();

        It should_resolve_instances_of_the_interface = () => _container.Resolve<IMyInterface>().ShouldBeOfType<IMyInterface>();
    }

    public class When_resolving_an_interface_implementation_should_allow_disambiguation_by_name
    {
        static TinyInversionOfControlContainer _container;

        Establish context = () =>
        {
            _container = new TinyInversionOfControlContainer(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>("FirstImpl");
            _container.Register<IMyInterface, MyOtherIntefaceImpl>("SecondImpl");
        };

        It should_resolve_instances_of_the_interface = () => _container.Resolve(typeof(IMyInterface), "FirstImpl").ShouldBeOfType<MyInterfaceImpl>();
    }

    public class When_resolving_an_interface_implementation_using_generics_should_allow_disambiguation_by_name
    {
        static TinyInversionOfControlContainer _container;

        Establish context = () =>
        {
            _container = new TinyInversionOfControlContainer(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>("FirstImpl");
            _container.Register<IMyInterface, MyOtherIntefaceImpl>("SecondImpl");
        };

        It should_resolve_instances_of_the_interface = () => _container.Resolve<IMyInterface>("FirstImpl").ShouldBeOfType<MyInterfaceImpl>();
    }
    public class When_resolving_an_interface_implementation_should_support_singleton
    {
        static TinyInversionOfControlContainer _container;

        Establish context = () =>
        {
            _container = new TinyInversionOfControlContainer(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>().AsSingleton();
        };

        It should_resolve_instances_of_the_interface = () => _container.Resolve(typeof(IMyInterface)).ShouldBeTheSameAs(_container.Resolve(typeof(IMyInterface)));
    }

    public class When_resolving_an_interface_implementation_should_support_multiple_implementations
    {
        static TinyInversionOfControlContainer _container;

        Establish context = () =>
        {
            _container = new TinyInversionOfControlContainer(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>().AsMultiInstance();
        };
        
        It should_resolve_instances_of_the_interface = () => _container.Resolve(typeof(IMyInterface)).ShouldNotBeTheSameAs(_container.Resolve(typeof(IMyInterface)));
    }

    internal class MyInterfaceImpl : IMyInterface {}
    internal class MyOtherIntefaceImpl : IMyInterface {}
    internal interface IMyInterface { }
}
