define(['durandal/plugins/router', 'durandal/viewLocator'],
    function (router, viewLocator) {
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
            }
        ];

        var config = {
           initialize : initialize, 
           routes: routes,
           startModule: 'welcome'
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