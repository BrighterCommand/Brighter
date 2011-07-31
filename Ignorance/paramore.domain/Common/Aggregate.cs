using System;
using Paramore.Infrastructure.Domain;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Domain.Common
{
    public class Aggregate : IAggregateRoot
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

        public Guid SisoId
        {
            get { return id; }
        }

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