using System;

namespace Paramore.Brighter
{
    internal interface IAmATransformLifetimeAsync : IDisposable
    {
        void Add(IAmAMessageTransformAsync transform);
    }
}
