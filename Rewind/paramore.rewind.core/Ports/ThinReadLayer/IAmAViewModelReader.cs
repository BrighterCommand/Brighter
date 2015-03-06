using System;
using System.Collections.Generic;
using Paramore.Rewind.Core.Adapters.Repositories;

namespace Paramore.Rewind.Core.Ports.ThinReadLayer
{
    public interface IAmAViewModelReader<out TDocument> where TDocument : IAmADocument
    {
        IEnumerable<TDocument> GetAll();
        TDocument Get(Guid id);
    }
}