using System;

namespace Paramore.Domain.Locations
{
    public class LocationMap
    {
        private readonly Uri map;

        public LocationMap(Uri map)
        {
            this.map = map;
        }
    }
}