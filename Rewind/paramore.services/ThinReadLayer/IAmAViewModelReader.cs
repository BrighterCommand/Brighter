using System.Collections.Generic;
using Paramore.Adapters.Infrastructure.Repositories;

namespace Paramore.Ports.Services.ThinReadLayer
{
    public interface IAmAViewModelReader<out TDocument > where TDocument : IAmADocument
    {
        IEnumerable<TDocument> GetAll();
    }
}