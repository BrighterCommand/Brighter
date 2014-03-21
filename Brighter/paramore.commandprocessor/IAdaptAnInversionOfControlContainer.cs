using System;
using Microsoft.Practices.ServiceLocation;

namespace paramore.commandprocessor
{
    public interface IAdaptAnInversionOfControlContainer : IServiceLocator, IDisposable
    {
        //Register
        IAdaptAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>()
            where RegisterType : class
            where RegisterImplementation : class, RegisterType;
        IAdaptAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>(string name) 
            where RegisterType : class 
            where RegisterImplementation : class, RegisterType;
        IAdaptAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>(RegisterImplementation instance)
            where RegisterType : class 
            where RegisterImplementation : class, RegisterType;

        IAdaptAnInversionOfControlContainer Register<RegisterImplementation>(string name, RegisterImplementation instance)
            where RegisterImplementation : class;

        //Declare Lifetime
        IAdaptAnInversionOfControlContainer AsMultiInstance();
        IAdaptAnInversionOfControlContainer AsSingleton();

        //Manage Lifetimes. Anything created within the scope, should be released on dispose (isntance or singleton)
        IAdaptAnInversionOfControlContainer  CreateScopedContainer();
    }
}
