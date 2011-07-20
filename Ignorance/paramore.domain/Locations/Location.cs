using System;
using Paramore.Infrastructure.Domain;

namespace Paramore.Domain.Locations
{
    public class Location : IAggregateRoot 
    {
        //private LocationName locationName;
        //private Address address;
        //private LocationMap map;
        //private LocationContact locationContact;
        private Guid id = Guid.Empty;
        private int version = 0;

        public Guid SisoId
        {
            get { return id; }
        }

        public int Version
        {
            get { return version; }
        }

        public int Lock(int expectedVersion)
        {
            return 0; 
        }
    }
}