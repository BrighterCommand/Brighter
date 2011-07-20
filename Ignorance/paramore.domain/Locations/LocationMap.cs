using System;

namespace Paramore.Domain.Locations
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