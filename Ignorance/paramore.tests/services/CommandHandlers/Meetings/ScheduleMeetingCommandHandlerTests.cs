using System;
using FakeItEasy;
using Machine.Specifications;
using Paramore.Domain.Meetings;
using Paramore.Infrastructure.Domain;
using Paramore.Services.CommandHandlers;
using Paramore.Services.Commands.Meeting;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Tests.services.CommandHandlers.Meetings
{
    [Subject("A create meeting command should result in creation of a new meeting")]
    public class ScheduleMeetingCommandHandlerTests
    {
        static ScheduleMeetingCommandHandler scheduleMeetingCommandHandler;
        static ScheduleMeetingCommand newMeetingRequest;
        static IRepository<Meeting> repository;
        static IMeetingFactory factory;
        static readonly Guid id = Guid.NewGuid();
        static readonly DateTime @on = DateTime.Today;
        static readonly Guid location = Guid.NewGuid();
        static readonly Guid speaker = Guid.NewGuid();
        private const int capacity = 100;

        Establish context = () =>
        {
            repository = A.Fake<IRepository<Meeting>>();
            factory = A.Fake<IMeetingFactory>();

            newMeetingRequest = new ScheduleMeetingCommand(id, @on, location, speaker, capacity);

            A.CallTo(() => factory.Schedule(new Id(id), new MeetingDate(@on), new Id(location), new Id(speaker), new Capacity(capacity)))
                .Returns(new Meeting(new MeetingDate(@on), new Id(location), new Id(speaker), new Capacity(capacity), new Version(), new Id(id)));

            scheduleMeetingCommandHandler = new ScheduleMeetingCommandHandler(repository);                                        
        };

        Because of = () => scheduleMeetingCommandHandler.Handle(command: newMeetingRequest);

        It should_add_a_meeting_to_the_repository = () => A.CallTo(() => repository.Add(A<Meeting>.Ignored)).MustHaveHappened();

        It should_ask_the_factory_to_create_an_instance_of_a_Meeting = () => 
            A.CallTo(() => factory.Schedule(new Id(id), new MeetingDate(@on), new Id(location), new Id(speaker), new Capacity(capacity))).MustHaveHappened();
    }
}
