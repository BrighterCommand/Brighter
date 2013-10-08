using System;
using FakeItEasy;
using Machine.Specifications;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Adapters.Tests.UnitTests.fakes;
using Paramore.Domain.Speakers;
using Paramore.Ports.Services.Commands.Speaker;
using Paramore.Ports.Services.Handlers.Speaker;

namespace Paramore.Adapters.Tests.UnitTests.services.CommandHandlers.Speakers
{
    [Subject("A call to the add speaker handler should result in a new speaker being added")]
    public class When_adding_a_new_speaker
    {
        static AddSpeakerCommandHandler addSpeakerCommandHandler;
        static AddSpeakerCommand addSpeakerCommand;
        static FakeRepository<Speaker, SpeakerDocument> speakerRepo;
        static IAmAUnitOfWorkFactory uoWFactory;
        static IUnitOfWork uow;

        Establish context = () =>
        {
            speakerRepo = new FakeRepository<Speaker, SpeakerDocument>();

            uoWFactory = A.Fake<IAmAUnitOfWorkFactory>();
            uow = A.Fake<IUnitOfWork>();

            A.CallTo(() => uoWFactory.CreateUnitOfWork()).Returns(uow);
            
            addSpeakerCommand = new AddSpeakerCommand(
                name: "The Dude",
                email: "dude@speakers.net",
                phoneNumber: "11111-111111",
                bio: "Alt.NET purse fighter");

            addSpeakerCommandHandler = new AddSpeakerCommandHandler(speakerRepo, uoWFactory);
        };

        static Speaker GetSpeakerFromRepoBy(Guid id)
        {
            return speakerRepo[id];
        }

        Because of = () => addSpeakerCommandHandler.Handle(addSpeakerCommand);

        It should_add_a_venue_to_the_repository = () => GetSpeakerFromRepoBy(addSpeakerCommand.Id).ShouldNotBeNull();
        It should_ask_the_session_factory_for_a_unit_of_work = () => A.CallTo(() => uoWFactory.CreateUnitOfWork()).MustHaveHappened();
        It should_commit_the_unit_of_work = () => A.CallTo(() => uow.Commit()).MustHaveHappened();
        It should_set_the_speaker_name = () => GetSpeakerFromRepoBy(addSpeakerCommand.Id).ToDocument().Name.ShouldEqual(addSpeakerCommand.Name);
        It should_set_the_speaker_bio = () => GetSpeakerFromRepoBy(addSpeakerCommand.Id).ToDocument().Bio.ShouldEqual(addSpeakerCommand.Bio);
        It should_set_the_speaker_phone_no = () => GetSpeakerFromRepoBy(addSpeakerCommand.Id).ToDocument().PhoneNumber.ShouldEqual(addSpeakerCommand.PhoneNumber);
        It should_set_the_speaker_email = () => GetSpeakerFromRepoBy(addSpeakerCommand.Id).ToDocument().Email.ShouldEqual(addSpeakerCommand.Email);
    }
}
