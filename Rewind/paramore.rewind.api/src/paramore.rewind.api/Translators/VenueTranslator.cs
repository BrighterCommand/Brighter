using System.Collections.Generic;
using System.Linq;
using paramore.rewind.adapters.presentation.api.Resources;

namespace paramore.rewind.adapters.presentation.api.Translators
{
    public class VenueTranslator : ITranslator<VenueResource, VenueDocument>
    {
        public VenueResource Translate(VenueDocument document)
        {
            return new VenueResource(
                document.Id, 
                document.Version,
                document.VenueName,
                document.Address, 
                document.VenueMap, 
                document.VenueContact);   
        }

        public List<VenueResource> Translate(List<VenueDocument> venues)
        {
            return venues.Select(venue => Translate(venue)).ToList();
        }
    }
}