using System;
using System.Collections.Generic;
using Paramore.Rewind.Core.Adapters.Repositories;
using Paramore.Rewind.Core.Domain.Speakers;
using Raven.Client.Linq;

namespace Paramore.Rewind.Core.Ports.ThinReadLayer
{
    public class SpeakerReader : IAmAViewModelReader<SpeakerDocument>
    {
        private readonly bool allowStale;
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;

        public SpeakerReader(IAmAUnitOfWorkFactory unitOfWorkFactory, bool allowStale = false)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.allowStale = allowStale;
        }

        public IEnumerable<SpeakerDocument> GetAll()
        {
            IRavenQueryable<SpeakerDocument> venues;
            using (IUnitOfWork unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                venues = unitOfWork.Query<SpeakerDocument>();
                if (!allowStale)
                {
                    venues.Customize(x => x.WaitForNonStaleResultsAsOfLastWrite());
                }
            }

            return venues;
        }

        public SpeakerDocument Get(Guid id)
        {
            using (IUnitOfWork unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                return unitOfWork.Load<SpeakerDocument>(id);
            }
        }
    }
}