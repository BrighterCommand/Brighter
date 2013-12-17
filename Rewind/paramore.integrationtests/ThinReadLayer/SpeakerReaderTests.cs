using System.Collections.Generic;
using System.Linq;
using Machine.Specifications;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Common;
using Paramore.Domain.Speakers;
using Paramore.Ports.Services.ThinReadLayer;

namespace paramore.integrationtests.ThinReadLayer
{
   [Subject("Check that we can get the speaker list out of the thin read layer")]
    public class When_viewing_a_list_of_speakers
   {
        private const string TEST_SPEAKER = "The Dude"; 
        private static IRepository<Speaker, SpeakerDocument> repository;
        private static IAmAUnitOfWorkFactory unitOfWorkFactory;
        private static IAmAViewModelReader<SpeakerDocument> reader; 
        private static IEnumerable<SpeakerDocument> speakers;
        private static Speaker speaker;
        
        Establish context = () =>
        {
            unitOfWorkFactory = new UnitOfWorkFactory();
            repository = new Repository<Speaker, SpeakerDocument>();
            speaker = new Speaker(
                id: new Id(), 
                version: new Version(), 
                bio: new SpeakerBio("ALT.NET purse fighter"), 
                phoneNumber: new PhoneNumber("11111-111111"), 
                emailAddress: new EmailAddress("dude@speakers.net"), 
                name: new SpeakerName(TEST_SPEAKER) );

            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                repository.UnitOfWork = unitOfWork;
                repository.Add(speaker);
                unitOfWork.Commit();
            }

            reader = new SpeakerReader(unitOfWorkFactory, false);
        };

        Because of = () => speakers = reader.GetAll();

        It should_return_the_test_speaker = () => speakers.Count(v => v.Name == TEST_SPEAKER).ShouldEqual(1);

        Cleanup cleanDownRepository = () =>
        {
            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                repository.UnitOfWork = unitOfWork;
                repository.Delete(speaker);
                unitOfWork.Commit();
            }                                                      
        };
    }

    [Subject("When retrieving a speaker by its id")]
    public class When_viewing_a_speaker_by_id
    {
        private const string TEST_SPEAKER = "The Dude"; 
        private static IRepository<Speaker, SpeakerDocument> repository;
        private static IAmAUnitOfWorkFactory unitOfWorkFactory;
        private static IAmAViewModelReader<SpeakerDocument> reader; 
        private static IEnumerable<SpeakerDocument> speakers;
        private static Speaker speaker;
        private static SpeakerDocument speakerDocument;
        
        Establish context = () =>
        {
            unitOfWorkFactory = new UnitOfWorkFactory();
            repository = new Repository<Speaker, SpeakerDocument>();
            speaker = new Speaker(
                id: new Id(), 
                version: new Version(), 
                bio: new SpeakerBio("ALT.NET purse fighter"), 
                phoneNumber: new PhoneNumber("11111-111111"), 
                emailAddress: new EmailAddress("dude@speakers.net"), 
                name: new SpeakerName(TEST_SPEAKER) );

            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                repository.UnitOfWork = unitOfWork;
                repository.Add(speaker);
                unitOfWork.Commit();
            }

            reader = new SpeakerReader(unitOfWorkFactory, false);
        };
        Because of = () => speakerDocument = reader.Get(speaker.Id);

        It should_return_a_venue = () => speakerDocument.ShouldNotBeNull();
        It should_return_the_matching_venue = () => speakerDocument.Id.ShouldEqual(speaker.Id);

        Cleanup cleanDownRepository = () =>
        {
            using (var unitOfWork = unitOfWorkFactory.CreateUnitOfWork())
            {
                repository.UnitOfWork = unitOfWork;
                repository.Delete(speaker);
                unitOfWork.Commit();
            }                                                      
        };

    }
}
