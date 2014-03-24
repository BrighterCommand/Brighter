using System.Collections.Generic;

namespace paramore.brighter.commandprocessor
{
    public interface IRequestContext
    {
        Dictionary<string, object> Bag { get; }
        IAdaptAnInversionOfControlContainer Container { get; set; }
    }
}