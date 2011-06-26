using NUnit.Framework;
using SpecUnit;
using UserGroupManagement.Domain.Common;
using UserGroupManagement.Domain.Momentos;
using UserGroupManagement.Domain.Speakers;

namespace UserGroupManagement.Tests.Domain.ConcerningSpeakers
{
    [Concern(typeof(Speaker))]
    [TestFixture]
    public class WhenCreatingAMementoForASpeaker : ContextSpecification
    {
        private const string SPEAKERNAME = "Test";
        private const string BIOGRAPHY = "MVP & Loud Mouth";
        private const string PHONENUMBER = "111 111 1111";
        private const string EMAIL = "foo@bar.com";
        private Speaker speaker;
        private SpeakerMemento memento;
 
        protected override void Context()
        {
            speaker = new SpeakerFactory().Create(new SpeakerName(SPEAKERNAME), new SpeakerBio(BIOGRAPHY), new PhoneNumber(PHONENUMBER), new EmailAddress(EMAIL));
        }

        protected override void Because()
        {
            memento = (SpeakerMemento)speaker.CreateMemento();
        }

        [Test]
        public void ShouldHaveTheSpeakerNameOnTheMemento()
        {
            memento.SpeakerName.ShouldEqual(SPEAKERNAME);
        }

        [Test]
        public void ShouldHaveTheSpeakerBioOnTheMemento()
        {
            memento.SpeakerBio.ShouldEqual(BIOGRAPHY);
        }

        [Test]
        public void ShouldHaveThePhoneNumberOnTheMemento()
        {
            memento.PhoneNumber.ShouldEqual(PHONENUMBER);
        }

        [Test]
        public void ShouldHaveEmailAddressOnThePhoneNumber()
        {
            memento.SpeakerEmail.ShouldEqual(EMAIL);
        }

    }
}
