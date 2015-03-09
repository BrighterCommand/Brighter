using System;
using System.Collections.Generic;
using System.Linq;
using OpenRasta.Web;
using paramore.commandprocessor;
using paramore.rewind.adapters.presentation.api.Resources;
using paramore.rewind.adapters.presentation.api.Translators;
using Paramore.Rewind.Core.Adapters.Repositories;
using Paramore.Rewind.Core.Domain.Common;
using Paramore.Rewind.Core.Domain.Venues;
using Paramore.Rewind.Core.Ports.Commands.Venue;
using Paramore.Rewind.Core.Ports.ThinReadLayer;
using Version = Paramore.Rewind.Core.Adapters.Repositories.Version;

namespace paramore.rewind.adapters.presentation.api.Handlers
{
    public class VenueEndPointHandler
    {
        private readonly IAmAUnitOfWorkFactory _unitOfWorkFactory;
        private readonly IAmACommandProcessor commandProcessor;

        public VenueEndPointHandler(IAmAUnitOfWorkFactory unitOfWorkFactory, IAmACommandProcessor commandProcessor)
        {
            _unitOfWorkFactory = unitOfWorkFactory;
            this.commandProcessor = commandProcessor;
        }

        public OperationResult Get()
        {
            var venues = new VenueTranslator().Translate(
                new VenueReader(_unitOfWorkFactory, false).GetAll().ToList()
                );
            //HACK!: var venues = Venues();

            return new OperationResult.OK
                    {
                        ResponseResource = venues
                    };
        }

        public OperationResult Post(VenueResource newVenueResource)
        {
            var addVenueCommand = new AddVenueCommand(
                venueName: newVenueResource.Name,
                address: newVenueResource.Address,
                mapURN: newVenueResource.MapURN,
                contact: newVenueResource.Contact);

            commandProcessor.Send(addVenueCommand);

            var venue = new VenueTranslator().Translate(
                new VenueReader(_unitOfWorkFactory, false)
                    .Get(addVenueCommand.Id)
                );
            
            return new OperationResult.Created()
                    {
                        ResponseResource = venue,
                        CreatedResourceUrl = new Uri(venue.Links[0].HRef)
                    };
        }

        public OperationResult Put(Guid id, VenueResource venueResource)
        {
            var updateVenueCommand = new UpdateVenueCommand(
                id: id,
                venueName: venueResource.Name,
                address: venueResource.Address,
                mapURN: venueResource.MapURN,
                contact: venueResource.Contact,
                version: venueResource.Version);

            commandProcessor.Send(updateVenueCommand);

            var venue = new VenueTranslator().Translate(
                new VenueReader(_unitOfWorkFactory, false)
                    .Get(updateVenueCommand.Id)
                );
            
            return new OperationResult.OK
                    {
                        ResponseResource = venue
                    };
        }

        public OperationResult Delete(Guid id)
        {
            var deleteVenueCommand = new DeleteVenueCommand(id);

            commandProcessor.Send(deleteVenueCommand);

            return new OperationResult.OK();
        }

        //HACK! method to get results without hitting Db
        private List<VenueResource> Venues()
        {
            var venues = new List<VenueResource>
                {
                    new VenueResource(
                        id: new Id(Guid.NewGuid()),
                        version: new Version(1),
                        name: new VenueName("Test Venue"),
                        address: new Address(new Street(1, "MyStreet"), new City("London"), new PostCode("N1 3GA")),
                        mapURN: new VenueMap(new Uri("http://www.mysite.com/maps/12345")),
                        contact:
                            new Contact(new Name("Ian"), new EmailAddress("ian@huddle.com"),
                                             new PhoneNumber("123454678")))
                };
            return venues;
        }
    }
}
