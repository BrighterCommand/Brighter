using System;
using Paramore.Domain.Common;

namespace Paramore.Domain.Venues
{
    public class VenueMap : IAmAValueType<Uri>
    {
        private readonly Uri map;

        public VenueMap(Uri map)
        {
            this.map = map;
        }

        public VenueMap()
        {
            map = new Uri("http://paramore/venue/map");
        }

        public Uri Value
        {
            get { return map; }
        }

        public static implicit operator string(VenueMap rhs)
        {
            return rhs.ToString();
        }

        public override string ToString()
        {
            return string.Format("{0}", map.AbsoluteUri);
        }
    }
}