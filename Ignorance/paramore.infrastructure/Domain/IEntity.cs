using System;
using Paramore.Infrastructure.Raven;

namespace Paramore.Infrastructure.Domain
{
    public interface IEntity<out T> where T : IAmADataObject
    {
        Id Id { get; }
        T ToDTO();
    }
}