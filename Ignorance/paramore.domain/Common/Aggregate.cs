using System;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Domain.Common
{
    public abstract class Aggregate<TDataObject> : IAmAnAggregateRoot<TDataObject> where TDataObject : IAmADataObject
    {
        protected Id id;
        protected Version version;

        public Aggregate(Id id, Version version)
        {
            this.id = id;
            this.version = version;
        }

        public Id Id
        {
            get { return id; }
        }

        public abstract TDataObject ToDTO();

        public Version Version
        {
            get { return version; }
        }

        public Version Lock(Version expectedVersion)
        {
            if (expectedVersion != version)
                throw new InvalidOperationException(string.Format("The version is out of date and cannot be updated. Expected {0} was {1}", expectedVersion, version));

            version++;

            return version;
        }
    }
}