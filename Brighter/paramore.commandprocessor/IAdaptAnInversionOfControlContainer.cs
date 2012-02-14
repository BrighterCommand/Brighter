using System;
using System.Collections.Generic;

namespace paramore.commandprocessor
{
    public interface IAdaptAnInversionOfControlContainer
    {
        IEnumerable<object> ResolveAll(Type resolveType, bool includeUnamed);
        object Resolve(Type resolveType);
        object Resolve(Type resolveType, string name);
        ResolveType Resolve<ResolveType>()
            where ResolveType : class;
        ResolveType Resolve<ResolveType>(string name)
            where ResolveType : class;

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
