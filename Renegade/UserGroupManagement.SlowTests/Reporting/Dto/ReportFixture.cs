using System;
using System.Linq;
using Fohjin.DDD.Reporting.Infrastructure;
using NUnit.Framework;
using UserGroupManagement.Configuration;
using UserGroupManagement.Reporting.Dto;

namespace UserGroupManagement.SlowTests.Reporting.Dto
{
    [TestFixture]
    public class ReportFixture
    {
        private SQLiteReportingRepository repository;
        private const string DATA_BASE_FILE = "reportingDataBase.db3";


        [SetUp]
        public void SetUp()
        {
            new ReportingDatabaseBootStrapper().ReCreateDatabaseSchema();

            var sqliteConnectionString = string.Format("Data Source={0}", DATA_BASE_FILE);

            repository = new SQLiteReportingRepository(sqliteConnectionString, new SqlSelectBuilder(), new SqlInsertBuilder(), new SqlUpdateBuilder(), new SqlDeleteBuilder());
        }

        [Test]
        public void ShouldBeAbleToSaveAndRetrieveAMeetingDto()
        {
            var meetingTime = DateTime.Now.ToString("yyMMMdd");
            var clientDto = new MeetingReport(Guid.NewGuid(), meetingTime);
            repository.Save(clientDto);
            var sut = repository.GetByExample<MeetingReport>(new { MeetingTime = meetingTime }).FirstOrDefault();

            Assert.That(sut.Id, Is.EqualTo(clientDto.Id));
            Assert.That(sut.MeetingTime, Is.EqualTo(clientDto.MeetingTime));
        }


        [Test]
        public void ShouldBeAbleToSaveAndRetrieveAMeetingDetailsDto()
        {
            var meetingTime = DateTime.Now.ToString("yyMMMdd");
            const int CAPACITY = 100;
            Guid locationId = Guid.NewGuid();
            Guid speakerId = Guid.NewGuid();
            var clientDto = new MeetingDetailsReport(Guid.NewGuid(), meetingTime, CAPACITY, locationId, speakerId);
            repository.Save(clientDto);
            var sut = repository.GetByExample<MeetingDetailsReport>(new { MeetingTime = meetingTime }).FirstOrDefault();

            Assert.That(sut.Id, Is.EqualTo(clientDto.Id));
            Assert.That(sut.MeetingTime, Is.EqualTo(clientDto.MeetingTime));
            Assert.That(sut.Capacity, Is.EqualTo(clientDto.Capacity));
            Assert.That(sut.LocationId, Is.EqualTo(clientDto.LocationId));
            Assert.That(sut.SpeakerId, Is.EqualTo(clientDto.SpeakerId));
        }
    }
}
