using System.Collections.Generic;
using System.Linq;
using paramore.rewind.adapters.presentation.api.Resources;
using Paramore.Rewind.Core.Domain.Speakers;

namespace paramore.rewind.adapters.presentation.api.Translators
{
    public class SpeakerTranslator : ITranslator<SpeakerResource, SpeakerDocument>
    {
        public SpeakerResource Translate(SpeakerDocument document)
        {
          return new SpeakerResource(
              document.Id,
              document.Version,
              document.Name,
              document.PhoneNumber,
              document.Email,
              document.Bio); 
        }

        public List<SpeakerResource> Translate(List<SpeakerDocument> speakers)
        {
            return speakers.Select(speaker => Translate(speaker)).ToList();
        }
    }
}