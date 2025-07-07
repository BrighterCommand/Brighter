using System;
using System.Collections.Generic;

namespace Paramore.Brighter;

public interface IAmAnInterpreter
{
    IEnumerable<Type> GetHandlers<TRequest>() where TRequest : class, IRequest;
}
