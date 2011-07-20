using Paramore.Domain.Common;

namespace Paramore.Domain.Locations
{
    public class LocationFactory
    {
        public Location Create(LocationName locationName, Address address, LocationMap map, LocationContact locationContact)
        {
            return new Location();
        }
    }
}