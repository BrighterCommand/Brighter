using System;
using NUnit.Framework;
using SpecUnit;
using UserGroupManagement.Domain.Meetings;
using UserGroupManagement.Domain.Momentos;

namespace UserGroupManagement.Tests.Domain.ConcerningMeetings
{
    [Concern(typeof(Meeting))]
    [TestFixture]
    public class WhenCreatingAMementoForAMeeting : ContextSpecification
    {
        private Meeting meeting;
        private static readonly Guid ID = Guid.NewGuid();
        private static readonly DateTime NOW = DateTime.Now;
        private static readonly Guid LOCATION_ID = Guid.NewGuid();
        private static readonly Guid SPEAKER_ID = Guid.NewGuid();
        private const int CAPACITY = 100;
        private MeetingMemento memento;

        protected override void Context()
        {
            meeting = new MeetingFactory().Schedule(ID, NOW, LOCATION_ID, SPEAKER_ID, CAPACITY);
        }

        protected override void Because()
        {
            memento = (MeetingMemento)meeting.CreateMemento();
        }

        [Test]
        public void ShouldHaveMeetingTimeOnMemento()
        {
            memento.MeetingTime.ShouldEqual(NOW);
        }

        [Test]
        public void ShouldHaveLocationIdOnMemento()
        {
            memento.LocationId.ShouldEqual(LOCATION_ID);
        }

        [Test]
        public void ShouldHaveSpeakerIdOnMemento()
        {
            memento.SpeakerId.ShouldEqual(SPEAKER_ID);
        }

        [Test]
        public void ShouldHaveCapacityOnMemento()
        {
            memento.Capacity.ShouldEqual(CAPACITY);
        }
    }
}
