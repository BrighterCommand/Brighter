using System;
using System.Collections.Generic;
using System.Linq;
using FakeItEasy;
using Machine.Specifications;
using Paramore.Domain.Venues;
using Paramore.Infrastructure.Repositories;
using Paramore.Services.ThinReadLayer;
using Version = Paramore.Infrastructure.Repositories.Version;

namespace paramore.integrationtests
{
    [Subject("Chekc that we can get the venue list out of the thin read layer")]
    public class When_viewing_a_list_of_venues
    {
        private const string TEST_VENUE = "Test Venue";
        private static IRepository<Venue, VenueDocument> repository;
        private static IAmAUnitOfWorkFactory unitOfWorkFactory;
        private static IViewModelReader<VenueDocument> reader; 
        private static IEnumerable<VenueDocument> venues;
        private static Venue venue;
        
        Establish context = () =>
        {
            repository = new Repository<Venue, VenueDocument>();
            venue = new Venue(id: new Id(), version: new Version(), venueName: new VenueName(TEST_VENUE));

            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                repository.UnitOfWork = unitOfWork;
                repository.Add(venue);
                unitOfWork.Commit();
            }
        };

        Because of = () => venues = reader.GetAll();

        It should_return_the_test_venue = () => venues.Count(v => v.VenueName == TEST_VENUE).ShouldEqual(1);

        Cleanup cleanDownRepository = () =>
        {
            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                repository.UnitOfWork = unitOfWork;
                repository.Delete(venue);
                unitOfWork.Commit();
            }                                                      
        };

    }
}
