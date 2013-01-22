using System;
using Paramore.Adapters.Infrastructure.Repositories;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Domain.Common
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
                throw new InvalidOperationException(string.Format("The version is out of date and cannot be updated. Expected {0} was {1}", expectedVersion, version));

            version++;

            return version;
        }

        public static implicit operator TDocument(AggregateRoot<TDocument> aggregateRoot)
        {
            return aggregateRoot.ToDocument();
        }

        protected abstract TDocument ToDocument();

        public Version Version
        {
            get { return version; }
        }


    }
}