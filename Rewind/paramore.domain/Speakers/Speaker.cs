using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Common;

namespace Paramore.Domain.Speakers
{
    public class Speaker : AggregateRoot<SpeakerDocument>
    {
        private SpeakerBio bio;
        private EmailAddress emailAddress;
        private SpeakerName name;
        private PhoneNumber phoneNumber;

        public Speaker(Id id, Version version, SpeakerBio bio, PhoneNumber phoneNumber, EmailAddress emailAddress, SpeakerName name) : base(id, version)
        {
            this.bio = bio;
            this.emailAddress = emailAddress;
            this.name = name;
            this.phoneNumber = phoneNumber;
        }

        public Speaker() : base(new Id(), new Version()){}

        #region Aggregate Persistence
        public override void Load(SpeakerDocument document)
        {
            bio = new SpeakerBio(document.Bio);
            emailAddress = new EmailAddress(document.Email);
            name = new SpeakerName(document.Name);
            phoneNumber = new PhoneNumber(document.PhoneNumber);
        }

        public override SpeakerDocument ToDocument()
        {
            return new SpeakerDocument(Id, Version, bio, phoneNumber, emailAddress, name);
        }

        #endregion

        public override string ToString()
        {
            return string.Format("Bio: {0}, EmailAddress: {1}, Name: {2}, PhoneNumber: {3}", bio, emailAddress, name, phoneNumber);
        }
    }
}