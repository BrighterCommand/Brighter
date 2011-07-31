using Paramore.Domain.Common;
using Paramore.Infrastructure.Domain;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Domain.Locations
{
    public class Location : Aggregate 
    {
        //private LocationName locationName;
        //private Address address;
        //private LocationMap map;
        //private LocationContact locationContact;
        public Location(Id id, Version version) : base(id, version)
        {
        }
    }
}