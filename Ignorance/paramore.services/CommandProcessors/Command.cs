using System;

namespace Paramore.Services.CommandProcessors
{
    [Serializable]
    public class Command : ICommand
    {
        public Guid Id { get; set; }

        public Command(Guid id)
        {
            Id = id;
        }
    }
}