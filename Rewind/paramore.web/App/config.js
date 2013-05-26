define(['durandal/plugins/router', 'durandal/viewLocator', 'services/mocks/mockVenues', 'services/mocks/mockSpeakers'],
    function (router, viewLocator, mockVenues, mockSpeakers) {
        var routes = [
            {
                url: 'venues',
                moduleId: 'viewmodels/venues',
                name: 'Venues',
                visible: true
            },
            {
                url: 'addVenueModal',
                moduleId: 'viewmodels/addVenueModal',
                name: 'AddVenues',
                visible: false
            },
            {
                url: 'speakers',
                moduleId: 'viewmodels/speakers',
                name: 'Speakers',
                visible: true
            }
        ];

        var requests = [
            {
                resourceId: 'venues',
                type: 'ajax',
                settings: {
                    url: "http://localhost:51872/venues",
                    //cache: "persist",
                    type: 'GET',
                    dataType: 'JSON',
                    contentType: 'application/vnd.paramore.data+json'
                }
            },
            {
                resourceId: 'addVenue',
                type: 'ajax',
                settings: {
                    url: "http://localhost:51872/venues",
                    type: 'POST',
                    dataType: 'JSON',
                    contentType: 'application/vnd.paramore.data+json',
                }
            },
            {
                resourceId: 'speakers',
                type: 'ajax',
                settings: {
                    url: "http://localhost:51872/speakers",
                    //cache:persist,
                    type: 'GET',
                    dataType: 'JSON',
                    contentType: 'application/vnd.paramore.data+json'
                }
            }
        ];

        var useMocks = false;
        var mockRequests = [
            {
                resourceId: 'venues',
                mockdata: mockVenues.data
            },
            {
                resourceId: 'speakers',
                mockdata: mockSpeakers.data
            }
        ];

        var config = {
            initialize: initialize,
            mockRequests : mockRequests,
            requests: requests,
            routes: routes,
            startModule: 'speakers',
            useMocks: useMocks
        };
        return config;
        
        function initialize() {
            //Replace 'viewmodels' in the moduleId with 'views' to locate the view.
            //Look for partial views in a 'views' folder in the root.
            viewLocator.useConvention();
            //configure routing
            router.useConvention();
        };
    });