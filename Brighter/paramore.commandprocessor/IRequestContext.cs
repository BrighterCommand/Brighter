using System.Collections.Generic;

namespace paramore.commandprocessor
{
    public interface IRequestContext
    {
        Dictionary<string, object> Bag { get; }
    }
}