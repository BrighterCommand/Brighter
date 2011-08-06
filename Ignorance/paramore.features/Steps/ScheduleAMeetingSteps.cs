using System;
using Castle.MicroKernel;
using Castle.MicroKernel.LifecycleConcerns;
using Castle.Windsor;
using Castle.MicroKernel.Registration;
using Paramore.Domain.Meetings;
using Paramore.Features.Tools;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;
using Paramore.Services.CommandHandlers;
using Paramore.Services.CommandHandlers.Meetings;
using Paramore.Services.CommandProcessors;
using Paramore.Services.Commands.Meeting;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Indexes;
using TechTalk.SpecFlow;

namespace Paramore.Features.Steps
{
    [Binding]
    public class ScheduleAMeetingSteps
    {
        private readonly ScheduleMeetingCommand scheduleMeetingCommand = new ScheduleMeetingCommand(Guid.NewGuid());
        private static CommandProcessor commandProcessor;
        private static WindsorContainer container;

        [BeforeFeature]
        public static void SetUp()
        {
            container = new WindsorContainer();
            container.AddFacility<Castle.Facilities.FactorySupport.FactorySupportFacility>();
            container.Register(Component.For<IDocumentStore>().ImplementedBy<DocumentStore>()
                                .DependsOn(new {connectionStringName = "RavenServer"})
                                .OnCreate(DoInitialisation)
                                .LifeStyle.Singleton,
                               Component.For<IUnitOfWork>().UsingFactoryMethod(GetUnitOfWork).LifeStyle.Transient,
                               Component.For<IAmAUnitOfWorkFactory>().ImplementedBy<UnitOfWorkFactory>().LifeStyle.Singleton,
                               Component.For<IRepository<Meeting, MeetingDTO>>().ImplementedBy<Repository<Meeting, MeetingDTO>>().LifeStyle.PerThread,
                               Component.For<IIssueTickets>().ImplementedBy<TicketIssuer>(),
                               Component.For<IAmAnOverbookingPolicy>().ImplementedBy<FiftyPercentOverbookingPolicy>().LifeStyle.Transient,
                               Component.For<IScheduler>().ImplementedBy<Scheduler>().LifeStyle.PerThread,
                               Component.For<IHandleRequests<ScheduleMeetingCommand>>().ImplementedBy<ScheduleMeetingCommandHandler>().LifeStyle.Transient);
            commandProcessor = new CommandProcessor(container);
        }

        static IUnitOfWork GetUnitOfWork(IKernel kernel)
        {
            var factory = kernel.Resolve<IAmAUnitOfWorkFactory>();
            return factory.CreateUnitOfWork();
        }


        public static void DoInitialisation(IKernel kernel, IDocumentStore store)
        {
            store.Initialize();
            //IndexCreation.CreateIndexes(typeof(EventSeries_ByName).Assembly, store);
        }

        [Given(@"I have a speaker (.*)")]
        public void GivenIHaveASpeaker(string speakerName)
        {
            //lookup the speaker - just use SQL to do this, via a thin read layer - grab the Id
        }

        [Given(@"I have a venue (.*)")]
        public void GivenIHaveAVenue(string venueName)
        {
            //lookkup the venue - just use SQL to do this, via a thin read layer - grab the id
        }

        [Given(@"I have a meeting date (.*)")]
        public void GivenIHaveAMeetingDate(string dateOfMeeting)
        {
            scheduleMeetingCommand.On = FuzzyDateTime.Parse(dateOfMeeting);
        }

        [Given(@"I have a capacity of (\d+)")]
        public void GivenIHaveACapacityOf(int seats)
        {
            scheduleMeetingCommand.Capacity = seats;
        }
 

        [When(@"I schedule a meeting")]
        public void WhenIScheduleAMeeting()
        {
            commandProcessor.Send(scheduleMeetingCommand);

            //how do we publish to report, directly or via command handler. Looks like by using transaction handler we go through unit of work whose commit method fires events to BUS
            //so if we have event and then save they get re-ublished and report canpick up
 
        }

        [Then(@"the new meeting should be open for registration")]
        public void ThenTheNewMeetingShouldBeOpenForRegistration()
        {
            //var sut = reportingRepository.GetByExample<MeetingDetailsReport>(new { MeetingTime = meetingDate }).FirstOrDefault();
        }

        [Then(@"the date should be (.*)")]
        public void ThenTheDateShouldBe(string dateOfMeeting)
        {
            ScenarioContext.Current.Pending();
        }

        [Then(@"(.*) tickets should be available")]
        public void ThenTicketsShouldBeAvailable(int noOfTickets)
        {
            ScenarioContext.Current.Pending();
        }


    }
}
