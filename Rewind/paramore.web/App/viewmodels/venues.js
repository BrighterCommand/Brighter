define(['durandal/system', 'durandal/app', 'durandal/data', 'durandal/logger'], function(system, app, data, logger) {
    var venueList= ko.observableArray([]);

   //The viewmodel
   var venues = {
        venueList: venueList,
        addVenue: addVenue,
        loadVenues: loadVenues,
        activate: activate
    };

   return venues;
    
    //for creating venues
    function Venue() {
        this.name = ko.observable(paramore.model.name);
        this.streetNumber = ko.observable();
        this.street = ko.observable();
        this.city = ko.observable();
        this.postcode = ko.observable();
        this.contactName = ko.observable();
        this.emailAddress = ko.observable();
        this.phoneNumber = ko.observable();
        this.map = ko.observable();
        this.self = ko.observable();
        this.version = ko.observable();

    };

    function addVenue() {
        venues.push(new Venue());
    };
    function loadVenues() {
        $.each(data.venues.rows, function(i, v) {
            venueList.push(new Venue()
                .name(v.name)
                .streetNumber(v.address.streetNumber)
                .street(v.address.street)
                .city(v.address.city)
                .postcode(v.address.postCode)
                .contactName(v.contact.name)
                .emailAddress(v.contact.emailAddress)
                .phoneNumber(v.contact.phoneNumber)
                .map(v.links[1].HRef)
                .self(v.links[0].HRef)
                .version(v.version)
            );
        });
    };
    function activate () {
        //the router's activator calls this function and waits for it to complete before proceding
        //Note: Data bind the values between the source and the targets using Knockout
        logger.log('Venue view activate',
                null,
                system.getModuleId(venueList),
                true);
            loadVenues();
        ko.applyBindings(venues);
    };

 
});