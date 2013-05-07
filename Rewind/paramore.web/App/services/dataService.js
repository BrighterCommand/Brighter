define(['config', 'services/dataService.venues'],
    function(config, venues) {
        var requests = config.requests;
        var mockRequests = config.mockRequests;
        var useMocks = config.useMocks;
        var dataService = {
            initialize: initialize,
            venues : venues
        };

        return dataService;

        function initialize() {
            if (!useMocks) {
                buildRequestDefinitions();
            } else {
                buildMockRequestDefinitions();
            }

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
            for (var i = 0; i < requests.length; i++) {
                var request = requests[i];
                amplify.request.define(
                    request.resourceId,
                    request.resource
                );
            }
        };
    });