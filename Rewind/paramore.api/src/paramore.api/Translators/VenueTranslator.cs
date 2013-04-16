using Paramore.Adapters.Presentation.API.Resources;
using Paramore.Domain.Venues;

namespace Paramore.Adapters.Presentation.API.Translators
{
    internal class VenueTranslator
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
    }
}