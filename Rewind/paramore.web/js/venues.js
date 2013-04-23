s$(function () {
 
    //for creating venues
    paramore.Venue = function () {
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

    //The viewmodel
    paramore.ViewModel = function () {
        var
            venues = ko.observableArray([]),
            addVenue = function() {
                venues.push(new paramore.Venue());
            },
            loadVenues = function() {
                $.each(paramore.model.data.Venues, function(i, v) {
                    venues.push(new paramore.Venue()
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
            }
        ;

        return {
            venues: venues,
            addVenue : addVenue,
            loadVenues : loadVenues
        };

    }();

    //Note: Data bind the values between the source and the targets using Knockout
    paramore.ViewModel.loadVenues();
    ko.applyBindings(paramore.ViewModel);
});