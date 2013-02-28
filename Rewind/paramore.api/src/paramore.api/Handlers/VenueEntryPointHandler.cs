using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenRasta.Web;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Common;
using Paramore.Domain.Venues;
using Paramore.Ports.Services.ThinReadLayer;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Adapters.Presentation.API.Handlers
{
    public class VenueEndPointHandler
    {
        private readonly IAmAUnitOfWorkFactory _unitOfWorkFactory;

        public VenueEndPointHandler(IAmAUnitOfWorkFactory unitOfWorkFactory)
        {
            _unitOfWorkFactory = unitOfWorkFactory;
        }

        public OperationResult Get()
        {
            //var venues = new VenueReader(_unitOfWorkFactory, false).GetAll().ToList();
            var venues = VenueDocuments();

            return new OperationResult.OK
                    {
                        ResponseResource = venues
                    };
        }

        //DEBUG method to get results without hitting Db
        private List<VenueDocument> VenueDocuments()
        {
            var venues = new List<VenueDocument>
                {
                    new VenueDocument(
                        id: new Id(Guid.NewGuid()),
                        version: new Version(1),
                        venueName: new VenueName("Test Venue"),
                        address: new Address(new Street("MyStreet"), new City("London"), new PostCode("N1 3GA")),
                        venueMap: new VenueMap(),
                        venueContact:
                            new VenueContact(new ContactName("Ian"), new EmailAddress("ian@huddle.com"),
                                             new PhoneNumber("123454678")))
                };
            return venues;
        }
    }
}
