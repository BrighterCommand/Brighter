using paramore.commandprocessor;
using Paramore.Rewind.Core.Adapters.Repositories;
using Paramore.Rewind.Core.Domain.Common;
using Paramore.Rewind.Core.Domain.Speakers;
using Paramore.Rewind.Core.Ports.Commands.Speaker;

namespace Paramore.Rewind.Core.Ports.Handlers.Speakers
{
    public class AddSpeakerCommandHandler : RequestHandler<AddSpeakerCommand>
    {
        private readonly IRepository<Speaker, SpeakerDocument> repository;
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;

        public AddSpeakerCommandHandler(IRepository<Speaker, SpeakerDocument> repository, IAmAUnitOfWorkFactory unitOfWorkFactory)
        {
            this.repository = repository;
            this.unitOfWorkFactory = unitOfWorkFactory;
        }

        public override AddSpeakerCommand Handle(AddSpeakerCommand command)
        {
            var speaker = new Speaker(
                phoneNumber: new PhoneNumber(command.PhoneNumber),
                bio: new SpeakerBio(command.Bio),
                emailAddress: new EmailAddress(command.Email),
                name: new SpeakerName(command.Name)
                );

            using (IUnitOfWork unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                repository.UnitOfWork = unitOfWork;
                repository.Add(speaker);
                unitOfWork.Commit();
            }

            command.Id = speaker.Id;

            return base.Handle(command);
        }
    }
}