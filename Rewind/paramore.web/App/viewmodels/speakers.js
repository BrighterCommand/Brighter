define(['durandal/system', 'durandal/app', 'services/dataService'], function (system, app, dataService) {
    var initialized = false,
        rows = [],
        speakerList = ko.observableArray([]),
        speakers = {
            speakerList: speakerList,
            loadSpeakers: load,
            addSpeaker: addSpeaker,
            activate: activate
        };
    return speakers;
    
    //create new Speakers
    function Speaker() {
        this.name = ko.observable();
        this.emailAddress = ko.observable();
        this.phoneNumber = ko.observable();
        this.bio = ko.observable();
        this.self = ko.observable();
    };
    
    function addSpeaker() {

    };

    function load() {
            dataService.speakers.getSpeakers()
            .then(
                function (data) {
                    rows = data;
                    system.log("Retrieved speakers from the Paramore API");
                    $.each(rows, function(i, v) {
                        speakerList.push(new Speaker()
                            .name(v.Name)
                            .emailAddress(v.EmailAddress)
                            .phoneNumber(v.PhoneNumber)
                            .bio(v.Bio)
                            .self(v.Links[0].HRef)
                        );
                    });
                    speakerList.sort(sortSpeakers);
                },
                function (data, status) {
                    system.log("Failed to load data: " + status, data);
                }
            );
    };
    
     function sortSpeakers(right, left) {
        return (right.name > left.name) ? 1 : -1;
    };

    function activate() {
        //the router's activator calls this function and waits for it to complete before proceding
        //Note: Data bind the values between the source and the targets using Knockout
        system.log('Activating the speakers viewmodel');
        if (initialized) {
            return;
        }

        load();
        initialized = true;
    };
});