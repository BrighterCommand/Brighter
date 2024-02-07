using System;

namespace Paramore.Brighter
{
    internal interface IAmATransformLifetime : IDisposable
    {
        void Add(IAmAMessageTransform transform);
    }
}
