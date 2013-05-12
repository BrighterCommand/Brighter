define(['durandal/plugins/router', 'durandal/viewLocator', 'services/mocks/mockVenues'],
    function (router, viewLocator, mockVenues) {
        var routes = [
            {
                url: 'welcome',
                moduleId: 'viewmodels/welcome',
                name: 'Welcome',
                visible: true
            },
            {
                url: 'filckr',
                moduleId: 'viewmodels/flickr',
                name: 'flickr',
                visible: true
            },
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
            }
        ];

        var requests = [
            {
                resourceId: 'venues',
                type: 'ajax',
                settings: {
                    url: "http://localhost:31290/venues",
                    //cache: "persist",
                    type: 'GET',
                    dataType: 'JSON'
                }
            }
        ];

        var useMocks = true;
        var mockRequests = [
            {
                resourceId: 'venues',
                mockdata: mockVenues.data
            }
        ];

        var config = {
            initialize: initialize,
            mockRequests : mockRequests,
            requests: requests,
            routes: routes,
            startModule: 'welcome',
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