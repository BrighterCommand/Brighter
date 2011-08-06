using Paramore.Domain.Common;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;

namespace Paramore.Tests.services.CommandProcessors.TestDoubles
{
    internal class MyEntity : Aggregate<MyEntityDTO>
    {
        public MyEntity(Id id, Version version) : base(id, version)
        {
        }

        public override MyEntityDTO ToDTO()
        {
            return new MyEntityDTO();
        }
    }

    internal class MyEntityDTO : IAmADataObject
    {
    }
}