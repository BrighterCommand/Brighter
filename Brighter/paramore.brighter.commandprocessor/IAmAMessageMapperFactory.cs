using System;

namespace paramore.brighter.commandprocessor
{
    public interface IAmAMessageMapperFactory
    {
        IAmAMessageMapper Create(Type messageMapperType);
    }
}