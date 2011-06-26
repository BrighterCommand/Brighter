using UserGroupManagement.Domain.Common;
using UserGroupManagement.Domain.Momentos;

namespace UserGroupManagement.Domain.Location
{
    public class LocationFactory
    {
        public Location Create(LocationName locationName, Address address, LocationMap map, LocationContact locationContact)
        {
            return new Location(locationName, address, map, locationContact);
        }
    }
}