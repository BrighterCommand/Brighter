using System;
using Paramore.Infrastructure.Domain;

namespace Paramore.Domain.Meetings
{
    public class Meeting : IAggregateRoot  
    {
        //private DateTime meeting;
        //private Guid locationId;
        //private Guid speakerId;
        //private int capacity;
        private Guid id = Guid.Empty;
        private int version = 0;

        public Guid SisoId
        {
            get { return id; }
        }

        public int Version
        {
            get { return version; }
        }

        public int Lock(int expectedVersion)
        {
            return 0;
        }
    }
}