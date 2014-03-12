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

        It should_create_a_wrapped_IoC_container = () => _container.ShouldBeAssignableTo<IAdaptAnInversionOfControlContainer>();
    }

    public class When_resolving_an_interface_implementation
    {
        static TinyIoCAdapter _container;

        Establish context = () => _container = new TinyIoCAdapter(new TinyIoCContainer());

        Because of = () => _container.Register<IMyInterface, MyInterfaceImpl>();

        It should_resolve_instances_of_the_interface = () => _container.GetInstance(typeof(IMyInterface)).ShouldBeAssignableTo<IMyInterface>();
    }

    public class When_resolving_an_interface_implementation_using_generic_shorthand
    {
        static TinyIoCAdapter _container;

        Establish context = () => _container = new TinyIoCAdapter(new TinyIoCContainer());

        Because of = () => _container.Register<IMyInterface, MyInterfaceImpl>();

        It should_resolve_instances_of_the_interface = () => _container.GetInstance<IMyInterface>().ShouldBeAssignableTo<IMyInterface>();
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

        It should_resolve_instances_of_the_interface = () => _container.GetInstance(typeof(IMyInterface), "FirstImpl").ShouldBeAssignableTo<MyInterfaceImpl>();
    }

    public class When_resolving_an_interface_implementation_should_allow_specific_instance
    {
        static TinyIoCAdapter _container;
        static readonly MyInterfaceImpl myInterfaceImpl = new MyInterfaceImpl();

        Establish context = () =>
        {
            _container = new TinyIoCAdapter(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>(myInterfaceImpl);
        };

        It should_resolve_instances_of_the_interface = () => _container.GetInstance(typeof (IMyInterface)).ShouldBeTheSameAs(myInterfaceImpl);
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

        It should_resolve_instances_of_the_interface = () => _container.GetInstance<IMyInterface>("FirstImpl").ShouldBeAssignableTo<MyInterfaceImpl>();
    }
    public class When_resolving_an_interface_implementation_should_support_singleton
    {
        static TinyIoCAdapter _container;

        Establish context = () =>
        {
            _container = new TinyIoCAdapter(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>().AsSingleton();
        };

        It should_resolve_instances_of_the_interface = () => _container.GetInstance(typeof(IMyInterface)).ShouldBeTheSameAs(_container.GetInstance(typeof(IMyInterface)));
    }

    public class When_resolving_an_interface_implementation_should_support_multiple_implementations
    {
        static TinyIoCAdapter _container;

        Establish context = () =>
        {
            _container = new TinyIoCAdapter(new TinyIoCContainer());
            _container.Register<IMyInterface, MyInterfaceImpl>().AsMultiInstance();
        };
        
        It should_resolve_instances_of_the_interface = () => _container.GetInstance(typeof(IMyInterface)).ShouldNotBeTheSameAs(_container.GetInstance(typeof(IMyInterface)));
    }

    internal class MyInterfaceImpl : IMyInterface {}
    internal class MyOtherIntefaceImpl : IMyInterface {}
    internal interface IMyInterface { }
}
