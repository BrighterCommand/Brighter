using System;
using Paramore.Rewind.Core.Adapters.Repositories;
using Version = Paramore.Rewind.Core.Adapters.Repositories.Version;

namespace Paramore.Rewind.Core.Domain.Common
{
    public abstract class AggregateRoot<TDocument> : IAmAnAggregateRoot<TDocument> where TDocument : IAmADocument
    {
        protected Id id;
        protected Version version;

        protected AggregateRoot(Id id, Version version)
        {
            this.id = id;
            this.version = version;
        }

        public Id Id
        {
            get { return id; }
        }

        public abstract void Load(TDocument document);

        public Version Lock(Version expectedVersion)
        {
            if (expectedVersion != version)
                throw new InvalidOperationException(string.Format("The version is out of date and cannot be updated. Expected {0} was {1}", expectedVersion,
                                                                  version));

            version++;

            return version;
        }

        public abstract TDocument ToDocument();

        public Version Version
        {
            get { return version; }
        }

        public static implicit operator TDocument(AggregateRoot<TDocument> aggregateRoot)
        {
            return aggregateRoot.ToDocument();
        }
    }
}