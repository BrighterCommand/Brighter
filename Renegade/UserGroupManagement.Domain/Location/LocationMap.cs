using System;

namespace UserGroupManagement.Domain.Location
{
    public class LocationMap
    {
        public Uri Map { get; private set; }

        public LocationMap(Uri map)
        {
            Map = map;
        }
    }
}