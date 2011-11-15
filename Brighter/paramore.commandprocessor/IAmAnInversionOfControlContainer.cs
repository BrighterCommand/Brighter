using System;
using System.Collections.Generic;

namespace paramore.commandprocessor.sharedinterfaces
{
    public interface IAmAnInversionOfControlContainer
    {
        IEnumerable<object> ResolveAll(Type resolveType, bool includeUnamed);
        object Resolve(Type resolveType);

        IAmAnInversionOfControlContainer Register<RegisterType, RegisterImplementation>()
            where RegisterType : class
            where RegisterImplementation : class, RegisterType;

        void AsMultiInstance();
    }
}
