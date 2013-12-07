using System;
using paramore.commandprocessor;

namespace Paramore.Ports.Services.Commands.Speaker
{
    public class AddSpeakerCommand: Command, IRequest
    {
        public string Name { get; private set; }
        public string Bio { get; private set; }
        public string PhoneNumber { get; private set; }
        public string Email { get; private set; }

        public AddSpeakerCommand(string name, string bio, string phoneNumber, string email) 
            : base(Guid.NewGuid())
        {
            Name = name;
            Bio = bio;
            PhoneNumber = phoneNumber;
            Email = email;
        }
    }
}