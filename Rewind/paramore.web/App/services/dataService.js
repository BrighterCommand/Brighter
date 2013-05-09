define(['config', 'services/dataService.venues'],
    function(config, venues) {
        var requests = config.requests;
        var mockRequests = config.mockRequests;
        var useMocks = config.useMocks;
        var initialized = false;
        
        var dataService = {
            initialize: initialize,
            venues : venues
        };

        return dataService;

        function initialize() {
             if (initialized ) {
                 return;
             }

            if (!useMocks) {
                buildRequestDefinitions();
            } else {
                buildMockRequestDefinitions();
            }

            initialized = true;

        };
        
        function buildRequestDefinitions() {
            for (var i = 0; i < requests.length; i++) {
                var request = requests[i];
                amplify.request.define(
                    request.resourceId,
                    request.type,
                    request.settings
                );
            }
        };
        
        function buildMockRequestDefinitions() {
            for (var i = 0; i < mockRequests.length; i++) {
                var request = requests[i];
                amplify.request.define(
                    request.resourceId,
                    request.resource
                );
            }
        };
    });