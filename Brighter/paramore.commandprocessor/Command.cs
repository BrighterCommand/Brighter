using System;
using System.Xml.Serialization;

namespace paramore.commandprocessor
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