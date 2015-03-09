using System;
using System.Collections.Generic;
using OpenRasta.Web;
using paramore.commandprocessor;
using paramore.rewind.adapters.presentation.api.Resources;
using Paramore.Rewind.Core.Adapters.Repositories;
using Version = Paramore.Rewind.Core.Adapters.Repositories.Version;

namespace paramore.rewind.adapters.presentation.api.Handlers
{
    public class SpeakerEndPointHandler
    {
        private readonly IAmAUnitOfWorkFactory _unitOfWorkFactory;
        private readonly IAmACommandProcessor commandProcessor;

        public SpeakerEndPointHandler(IAmAUnitOfWorkFactory unitOfWorkFactory, IAmACommandProcessor commandProcessor)
        {
            _unitOfWorkFactory = unitOfWorkFactory;
            this.commandProcessor = commandProcessor;
        }

        public OperationResult Get()
        {
            /*
                 var speakers = new SpeakerTranslator().Translate(
                new SpeakerReader(_unitOfWorkFactory, false).GetAll().ToList()
                );
             */
            var speakers = Speakers();

            return new OperationResult.OK
                    {
                        ResponseResource = speakers
                    };
        }

        private List<SpeakerResource> Speakers()
        {
            var speakers = new List<SpeakerResource>()
            {
                new SpeakerResource(
                    id: new Id(Guid.NewGuid()),
                    version: new Version(1),
                    name: "Oscar Grouch",
                    phoneNumber: "666-666-6666",
                    emailAddress: "grouch@sesamestreet.com",
                    bio: "Oscar the Grouch is a Muppet character on the television program Sesame Street. He has a green body (during the first season he was orange), has no visible nose, and lives in a trash can. His favorite thing in life is trash, as evidenced by the song 'I Love Trash'."
                    )
            };

            return speakers;
        }
    }
}