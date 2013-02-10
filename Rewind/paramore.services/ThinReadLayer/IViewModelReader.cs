using System.Collections.Generic;
using Paramore.Infrastructure.Repositories;

namespace Paramore.Services.ThinReadLayer
{
    public interface IViewModelReader<out TDocument> where TDocument : IAmADocument
    {
        IEnumerable<TDocument> GetAll();
    }
}