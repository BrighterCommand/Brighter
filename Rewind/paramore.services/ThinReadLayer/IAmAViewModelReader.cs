using System;
using System.Collections.Generic;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Venues;

namespace Paramore.Ports.Services.ThinReadLayer
{
    public interface IAmAViewModelReader<out TDocument > where TDocument : IAmADocument
    {
        IEnumerable<TDocument> GetAll();
        TDocument Get(Guid id);
    }
}