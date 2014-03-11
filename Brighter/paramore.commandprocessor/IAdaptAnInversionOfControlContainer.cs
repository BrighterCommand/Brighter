using Microsoft.Practices.ServiceLocation;

namespace paramore.commandprocessor
{
    public interface IAdaptAnInversionOfControlContainer : IServiceLocator
    {
        IAdaptAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>()
            where RegisterType : class
            where RegisterImplementation : class, RegisterType;
        IAdaptAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>(string name) 
            where RegisterType : class 
            where RegisterImplementation : class, RegisterType;
        IAdaptAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>(RegisterImplementation instance)
            where RegisterType : class 
            where RegisterImplementation : class, RegisterType;

        void AsMultiInstance();
        void AsSingleton();
    }
}
