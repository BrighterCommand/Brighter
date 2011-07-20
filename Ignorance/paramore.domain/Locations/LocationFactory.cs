using UserGroupManagement.Domain.Common;

namespace UserGroupManagement.Domain.Locations
{
    public class LocationFactory
    {
        public Location Create(LocationName locationName, Address address, LocationMap map, LocationContact locationContact)
        {
            return new Location(locationName, address, map, locationContact);
        }
    }
}