using System;
using FakeItEasy;
using Machine.Specifications;
using Paramore.Domain.Meetings;
using Paramore.Domain.Venues;
using Paramore.Infrastructure.Repositories;

namespace Paramore.Tests.domain.Meetings
{
    [Subject(typeof(Scheduler))]
    public class When_we_schedule_a_meeting_there_should_be_tickets_over_capacity
    {
        static Scheduler scheduler;
        static Capacity capacity;
        static IAmAnOverbookingPolicy _overbookingPolicy;

        Establish context = () =>
                                {
                                    capacity = new Capacity(10);
                                    _overbookingPolicy = A.Fake<IAmAnOverbookingPolicy>();
                                    var tickets = new Tickets(capacity);
                                    A.CallTo(() => _overbookingPolicy.AllocateTickets(capacity)).Returns(tickets);

                                    scheduler = new Scheduler(_overbookingPolicy);

                                };

        Because of = () => scheduler.Schedule(new Id(Guid.NewGuid()), new MeetingDate(DateTime.Today), new Id(Guid.NewGuid()), new Id(Guid.NewGuid()), capacity);

        It should_call_the_booking_policy_to_allocate_tickets = () => A.CallTo(() => _overbookingPolicy.AllocateTickets(capacity)).MustHaveHappened();

    }

    [Subject(typeof(Scheduler))]
    public class When_we_schedule_a_meeting_it_should_have_a_time
    {
        static Scheduler scheduler;
        static IAmAnOverbookingPolicy _overbookingPolicy;
        static Exception exception;

        Establish context = () =>
        {
            _overbookingPolicy = A.Fake<IAmAnOverbookingPolicy>();
            scheduler = new Scheduler(_overbookingPolicy);
        };

        Because of = () => exception = Catch.Exception(() => scheduler.Schedule(new Id(Guid.NewGuid()), null, new Id(Guid.NewGuid()), new Id(Guid.NewGuid()), new Capacity(100)));

        It should_throw_an_invalid_argument_exception = () => exception.ShouldBeOfType<ArgumentNullException>();

        It should_indicate_the_reason_for_the_failure = () => exception.ShouldContainErrorMessage("A meeting must have a date to be scheduled");
    }

    [Subject(typeof(Scheduler))]
    public class When_we_schedule_a_meeting_we_only_need_a_time
    {
        static Scheduler scheduler;
        static Capacity capacity;
        static Meeting meeting;
        static IAmAnOverbookingPolicy _overbookingPolicy;

        Establish context = () =>
        {
            capacity = new Capacity(10);
            _overbookingPolicy = A.Fake<IAmAnOverbookingPolicy>();
            var tickets = new Tickets(capacity);
            A.CallTo(() => _overbookingPolicy.AllocateTickets(capacity)).Returns(tickets);

            scheduler = new Scheduler(_overbookingPolicy);

        };

        Because of = () => meeting = scheduler.Schedule(new Id(Guid.NewGuid()), new MeetingDate(DateTime.Today), null, null, null);

        It should_have_a_valid_meeting = () => meeting.ShouldNotBeNull();
    }

    public class When_we_schedule_a_meeting_it_should_be_open_for_registration
    {
        static Scheduler scheduler;
        static Capacity capacity;
        static Meeting meeting;
        static IAmAnOverbookingPolicy _overbookingPolicy;

        Establish context = () =>
        {
            capacity = new Capacity(10);
            _overbookingPolicy = A.Fake<IAmAnOverbookingPolicy>();
            var tickets = new Tickets(capacity);
            A.CallTo(() => _overbookingPolicy.AllocateTickets(capacity)).Returns(tickets);

            scheduler = new Scheduler(_overbookingPolicy);

        };

        Because of = () => meeting = scheduler.Schedule(new Id(Guid.NewGuid()), new MeetingDate(DateTime.Today), null, null, null);

        It should_be_open_for_registration = () => ((MeetingDocument)meeting).State.ShouldEqual(MeetingState.Live);
    }

    public class When_we_add_speaker_or_venue_they_should_be_noted
    {
        static Scheduler scheduler;
        static Capacity capacity;
        static Meeting meeting; 
        static IAmAnOverbookingPolicy _overbookingPolicy;
        static readonly Id speakerId = new Id(Guid.NewGuid());
        static readonly Id venueId = new Id(Guid.NewGuid());

        Establish context = () =>
                                {
                                    capacity = new Capacity(10);
                                    _overbookingPolicy = A.Fake<IAmAnOverbookingPolicy>();
                                    var tickets = new Tickets(capacity);
                                    A.CallTo(() => _overbookingPolicy.AllocateTickets(capacity)).Returns(tickets);

                                    scheduler = new Scheduler(_overbookingPolicy);

                                };

        Because of = () => meeting = scheduler.Schedule(new Id(Guid.NewGuid()), new MeetingDate(DateTime.Today), venueId, speakerId, capacity);

        It should_have_a_speaker_id = () => ((MeetingDocument)meeting).Speaker.ShouldEqual((Guid) speakerId);
        It should_have_a_venue_id = () => ((MeetingDocument)meeting).Venue.ShouldEqual((Guid) venueId);

    }
}
