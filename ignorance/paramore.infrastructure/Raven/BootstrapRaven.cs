using System;
using Raven.Client.Document;

namespace Paramore.Infrastructure.Raven
{
    public class RavenConnection
    {
        public IAmAUnitOfWorkFactory Connect(Uri documentStoreUri)
        {
            return new UnitOfWorkFactory(new DocumentStore {Url = documentStoreUri.AbsoluteUri});
        }
    }
}
