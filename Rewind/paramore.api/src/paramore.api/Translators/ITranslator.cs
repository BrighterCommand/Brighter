using System.Collections.Generic;
using Paramore.Adapters.Infrastructure.Repositories;

namespace Paramore.Adapters.Presentation.API.Translators
{
    internal interface ITranslator<TResource, TDocument> 
        where TDocument: IAmADocument
    {
        TResource Translate(TDocument document);
        List<TResource> Translate(List<TDocument> documents);
    }
}