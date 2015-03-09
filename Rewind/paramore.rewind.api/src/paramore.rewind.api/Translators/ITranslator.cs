using System.Collections.Generic;
using Paramore.Rewind.Core.Adapters.Repositories;

namespace paramore.rewind.adapters.presentation.api.Translators
{
    internal interface ITranslator<TResource, TDocument> 
        where TDocument: IAmADocument
    {
        TResource Translate(TDocument document);
        List<TResource> Translate(List<TDocument> documents);
    }
}