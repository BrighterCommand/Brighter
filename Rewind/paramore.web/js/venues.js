$(function () {
 
    //for creating venues
    paramore.Venue = function () {
        this.name = ko.observable(paramore.model.name);
        this.address = ko.observable();
        this.contact = ko.observable();
        this.map = ko.observable();
        this.self = ko.observable();
        this.version = ko.observable();

    };

    //The viewmodel
    paramore.ViewModel = function () {
        var
            venues = ko.observableArray([]),
            loadVenues = function() {
                $.each(paramore.model.data.Venues, function(i, v) {
                    venues.push(new paramore.Venue()
                        .name(v.name)
                        .address(v.address)
                        .contact(v.contact)
                        .map(v.links[1].HRef)
                        .self(v.links[0].HRef)
                        .version(v.version)
                    );
                });
            }
        ;

        return {
            venues: venues,
            loadVenues : loadVenues
        };

    }();
    

    //Note: Data bind the values between the source and the targets using Knockout
    paramore.ViewModel.loadVenues();
    ko.applyBindings(paramore.ViewModel);
});