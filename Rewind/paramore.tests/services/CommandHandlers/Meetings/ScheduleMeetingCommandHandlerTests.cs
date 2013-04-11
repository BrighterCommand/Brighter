using System;
using FakeItEasy;
using Machine.Specifications;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Meetings;
using Paramore.Domain.Venues;
using Paramore.Ports.Services.Commands.Meeting;
using Paramore.Ports.Services.Handlers.Meetings;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Adapters.Tests.UnitTests.services.CommandHandlers.Meetings
{
    [Subject("A create meeting command should result in creation of a new meeting")]
    public class When_a_create_meeting_command_is_received
    {
        static ScheduleMeetingCommandHandler scheduleMeetingCommandHandler;
        static ScheduleMeetingCommand newMeetingRequest;
        static IRepository<Meeting, MeetingDocument> repository;
        static IScheduler scheduler;
        static IAmAUnitOfWorkFactory uoWFactory;
        static IUnitOfWork uow;
        static readonly Guid id = Guid.NewGuid();
        static readonly DateTime @on = DateTime.Today;
        static readonly Guid location = Guid.NewGuid();
        static readonly Guid speaker = Guid.NewGuid();
        private const int capacity = 100;

        Establish context = () =>
        {
            repository = A.Fake<IRepository<Meeting, MeetingDocument>>();
            scheduler = A.Fake<IScheduler>();
            uoWFactory = A.Fake<IAmAUnitOfWorkFactory>();
            uow = A.Fake<IUnitOfWork>();

            newMeetingRequest = new ScheduleMeetingCommand(id, @on, location, speaker, capacity);

            A.CallTo(() => scheduler.Schedule(new Id(id), new MeetingDate(@on), new Id(location), new Id(speaker), new Capacity(capacity)))
                .Returns(new Meeting(new MeetingDate(@on), new Id(location), new Id(speaker), new Tickets(new Capacity(capacity)), new Version(), new Id(id)));

            A.CallTo(() => uoWFactory.CreateUnitOfWork()).Returns(uow);

            scheduleMeetingCommandHandler = new ScheduleMeetingCommandHandler(scheduler, repository, uoWFactory);                                        
        };

        Because of = () => scheduleMeetingCommandHandler.Handle(command: newMeetingRequest);

        It should_add_a_meeting_to_the_repository = () => A.CallTo(() => repository.Add(A<Meeting>.Ignored)).MustHaveHappened();
        It should_ask_the_factory_to_create_an_instance_of_a_Meeting = () =>  A.CallTo(() => scheduler.Schedule(new Id(id), new MeetingDate(@on), new Id(location), new Id(speaker), new Capacity(capacity))).MustHaveHappened();
        It should_ask_the_session_factory_for_a_unit_of_work = () => A.CallTo(() => uoWFactory.CreateUnitOfWork()).MustHaveHappened();
        It should_commit_the_unit_of_work = () => A.CallTo(() => uow.Commit()).MustHaveHappened();

    }
}
