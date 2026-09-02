using System;

namespace Paramore.Brighter
{
    internal interface IAmATransformLifetimeAsync : IDisposable, IAsyncDisposable
    {
        void Add(Lease<IAmAMessageTransformAsync> lease);
    }
}
