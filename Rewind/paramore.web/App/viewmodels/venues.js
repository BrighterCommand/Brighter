define(['durandal/system', 'durandal/app', 'services/dataService'], function (system, app, dataService) {
    var rows = [],
    venueList = ko.observableArray([]),
    initialized = false;
    //The viewmodel
    var venues = {
        venueList: venueList,
        addVenue: addVenue,
        loadVenues: load,
        activate: activate
    };

    return venues;

    //for creating venues
    function Venue() {
        this.name = ko.observable();
        this.buildingNumber = ko.observable();
        this.streetName = ko.observable();
        this.city = ko.observable();
        this.postcode = ko.observable();
        this.contactName = ko.observable();
        this.emailAddress = ko.observable();
        this.phoneNumber = ko.observable();
        this.map = ko.observable();
        this.self = ko.observable();
        this.version = ko.observable();

        this.toResource = function() {
            //{"address":{"city":"London","postCode":"EC1Y 2BP","streetName":"City Road","buildingNumber":"100"},   "contact":{"emailAddress":"ian@huddle.com","name":"Ian","phoneNumber":"123454678"},   "links":[{"HRef":"\/\/localhost:59280\/venue\/8b8c66fc-d541-4051-94ed-1699209d69b0","Rel":"self"}, {"HRef":"http:\/\/goo.gl\/maps\/nwJl7","Rel":"map"}], "name":"Test Venue", "version":1}
            return {
                name: this.name(),
                version: this.version(),
                address: {
                    city: this.city(),
                    postCode: this.postCode,
                    streetName: this.streetName,
                    buildingNumber: this.buildingNumber
                },
                contact: {
                    name: this.contactName(),
                    emailAddress: this.emailAddress(),
                    phoneNumber: this.phoneNumber
                },
                links: [
                    {
                        Rel: 'self',
                        Href: this.self()
                    },
                    {
                        Rel: 'map',
                        Href: this.map()
                    }
                ]
            };
        };
    };

    function addVenue() {
        app.showModal('viewmodels/addVenueModal')
            .then(function (response) {
                //update locally
                var newVenue = new Venue()
                    .name(response.name())
                    .buildingNumber(response.buildingNumber())
                    .streetName(response.streetName())
                    .city(response.city())
                    .postcode(response.postcode())
                    .contactName(response.contactName())
                    .emailAddress(response.emailAddress())
                    .phoneNumber(response.phoneNumber())
                    .map(response.map())
                    .self(response.self())
                    .version(0);
                venueList.push(newVenue);
                venueList.sort(sortVenues);

                //post to server
                dataService.venues.addVenue(newVenue.toResource())
                    .then(
                        function(data) {
                            system.log("added new venue" + data);
                        },
                        function (data, status) {
                            system.log("Failed to upload data: " + status, data);
                        }
                    );
            });
    };
    

    function load() {
        dataService.venues.getVenues()
            .then(
                function (data) {
                    rows = data;
                    system.log("Retrieved venues from the Paramore API");
                    $.each(rows, function (i, v) {
                        venueList.push(new Venue()
                            .name(v.name)
                            .buildingNumber(v.address.buildingNumber)
                            .streetName(v.address.streetName)
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
                    venueList.sort(sortVenues);
                },
                function (data, status) {
                    system.log("Failed to load data: " + status, data);
                }
            );
    };

    function sortVenues(right, left) {
        return (right.name > left.name) ? 1 : -1;
    };

    function activate() {
        //the router's activator calls this function and waits for it to complete before proceding
        //Note: Data bind the values between the source and the targets using Knockout
        system.log('Activating the venues viewmodel');
        if (initialized) {
            return;
        }

        load();
        initialized = true;
    };


});