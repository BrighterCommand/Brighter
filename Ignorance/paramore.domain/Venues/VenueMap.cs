using System;

namespace Paramore.Domain.Venues
{
    public class VenueMap
    {
        private readonly Uri map;

        public VenueMap(Uri map)
        {
            this.map = map;
        }

        public VenueMap()
        {
            map = new Uri(string.Empty);
        }

        public static implicit operator string(VenueMap venueMap)
        {
            return venueMap.map.AbsoluteUri;
        }
    }
}