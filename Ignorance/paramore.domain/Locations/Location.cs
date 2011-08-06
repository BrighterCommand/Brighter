using Paramore.Domain.Common;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Domain.Locations
{
    public class Location : Aggregate<LocationDTO> 
    {
        private LocationName locationName;
        private Address address;
        private LocationMap map;
        private LocationContact locationContact;

        public Location(Id id, Version version, LocationName locationName, Address address, LocationMap map, LocationContact locationContact) : base(id, version)
        {
            this.locationName = locationName;
            this.address = address;
            this.map = map;
            this.locationContact = locationContact;
        }

        public override LocationDTO ToDTO()
        {
            throw new System.NotImplementedException();
        }
    }

    public class LocationDTO : IAmADataObject
    {
    }
}