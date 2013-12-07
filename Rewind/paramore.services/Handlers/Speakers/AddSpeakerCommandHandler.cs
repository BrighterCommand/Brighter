using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Common;
using Paramore.Domain.Speakers;
using Paramore.Ports.Services.Commands.Speaker;
using paramore.commandprocessor;

namespace Paramore.Ports.Services.Handlers.Speakers
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

            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
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