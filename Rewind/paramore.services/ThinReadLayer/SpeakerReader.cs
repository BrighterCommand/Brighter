using System;
using System.Collections.Generic;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Speakers;
using Raven.Client.Linq;

namespace Paramore.Ports.Services.ThinReadLayer
{
    public class SpeakerReader: IAmAViewModelReader<SpeakerDocument>
    {
        private readonly IAmAUnitOfWorkFactory unitOfWorkFactory;
        private readonly bool allowStale;

        public SpeakerReader(IAmAUnitOfWorkFactory unitOfWorkFactory, bool allowStale = false)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.allowStale = allowStale;
        }

        public IEnumerable<SpeakerDocument> GetAll()
        {
            IRavenQueryable<SpeakerDocument> venues;
            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
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
            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                return unitOfWork.Load<SpeakerDocument>(id);
            }
        }
    }
}
