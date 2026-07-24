using System;

namespace Paramore.Brighter
{
    internal interface IAmATransformLifetimeAsync : IDisposable, IAsyncDisposable
    {
        void Add(IAmAMessageTransformAsync transform);
    }
}
