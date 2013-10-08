using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Speakers;
using Paramore.Ports.Services.Commands.Speaker;
using paramore.commandprocessor;

namespace Paramore.Ports.Services.Handlers.Speaker
{
    public class AddSpeakerCommandHandler: RequestHandler<AddSpeakerCommand>
    {
        private readonly IRepository<Domain.Speakers.Speaker, SpeakerDocument> speakerRepo;
        private readonly IAmAUnitOfWorkFactory uoWFactory;

        public AddSpeakerCommandHandler(IRepository<Domain.Speakers.Speaker, SpeakerDocument> speakerRepo, IAmAUnitOfWorkFactory uoWFactory)
        {
            this.speakerRepo = speakerRepo;
            this.uoWFactory = uoWFactory;
        }
    }
}