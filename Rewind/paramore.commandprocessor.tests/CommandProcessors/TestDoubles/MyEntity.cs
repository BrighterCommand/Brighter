using Paramore.Domain.Common;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyEntity : Aggregate<MyEntityDTO>
    {
        public MyEntity(Id id, Version version) : base(id, version)
        {
        }

        public MyEntity() : base(new Id(), new Version()) {}

        public override void Load(MyEntityDTO dataObject)
        {
            throw new System.NotImplementedException();
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