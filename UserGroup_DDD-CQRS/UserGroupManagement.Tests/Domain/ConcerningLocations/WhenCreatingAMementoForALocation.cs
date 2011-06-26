using System;
using NUnit.Framework;
using SpecUnit;
using UserGroupManagement.Domain.Common;
using UserGroupManagement.Domain.Location;
using UserGroupManagement.Domain.Momentos;

namespace UserGroupManagement.Tests.Domain.ConcerningLocations
{
    [Concern(typeof(Location))]
    [TestFixture]
    public class WhenCreatingAMementoForALocation : ContextSpecification
    {
        private const string STREET_NUMBER = "213";
        private const string STREET = "Acacia Ave";
        private const string CITY = "Anytown";
        private const string POSTAL_CODE = "ST9 64G";
        private const string LOCATION_NAME = "Our place";
        private const string MAP_URI = @"http://www.dnug.org.uk";
        private const string CONTACT_NAME = "Helpful Person";
        private const string EMAIL_ADDRESS = "helpful.person@sponsors.com";
        private const string PHONE_NUMBER = "222 222 2222";
        private Location location;
        private LocationMemento locationMemento;

        protected override void Context()
        {
            var venueName = new LocationName(LOCATION_NAME);
            var address = new Address(STREET_NUMBER, STREET, CITY, POSTAL_CODE);
            var map = new LocationMap(new Uri(MAP_URI));
            var locationContact = new LocationContact(new ContactName(CONTACT_NAME),
                                                      new EmailAddress(EMAIL_ADDRESS),
                                                      new PhoneNumber(PHONE_NUMBER));

            location = new LocationFactory().Create(venueName, address, map, locationContact);
        }

        protected override void Because()
        {
            locationMemento = (LocationMemento) location.CreateMemento();
        }

        [Test]
        public void ShouldHaveLocationNameOnMemento()
        {
            locationMemento.LocationName.ShouldEqual(LOCATION_NAME);
        }

        [Test]
        public void ShouldHaveLocationStreetNumberOnMemento()
        {
            locationMemento.LocationStreetNumber.ShouldEqual(STREET_NUMBER);
        }

        [Test]
        public void ShouldHaveLocationStreetOnMemento()
        {
            locationMemento.LocationStreet.ShouldEqual(STREET);
        }

        [Test]
        public void ShouldHaveLocationCityOnMemento()
        {
            locationMemento.LocationCity.ShouldEqual(CITY);
        }

        [Test]
        public void ShouldHaveLocationPostalCodeOnMemento()
        {
            locationMemento.LocationPostalCode.ShouldEqual(POSTAL_CODE);
        }

        [Test]
        public void ShouldHaveLocationMapOnMemento()
        {
            locationMemento.Map.ShouldEqual(MAP_URI);
        }

        [Test]
        public void ShouldHaveLocationContactName()
        {
            locationMemento.ContactName.ShouldEqual(CONTACT_NAME);
        }

        [Test]
        public void ShouldHaveContactEmailAddress()
        {
            locationMemento.ContactEmail.ShouldEqual(EMAIL_ADDRESS);
        }

        [Test]
        public void ShouldHaveAContactPhoneNumber()
        {
            locationMemento.ContactPhoneNumber.ShouldEqual(PHONE_NUMBER);
        }

    }
}
