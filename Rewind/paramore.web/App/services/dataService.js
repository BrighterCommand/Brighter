define(['config', 'services/dataService.venues', 'services/dataService.speakers'],
    function(config, venues, speakers) {
        var requests = config.requests;
        var mockRequests = config.mockRequests;
        var useMocks = config.useMocks;
        var initialized = false;
        
        var dataService = {
            initialize: initialize,
            venues: venues,
            speakers: speakers
        };

        return dataService;

        function initialize() {
             if (initialized ) {
                 return;
             }

             buildRequestDefinitions();
            
             if (useMocks) {
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
                var request = mockRequests[i];
                amplify.request.define(
                    request.resourceId,
                    function(resource) {
                        resource.success(request.mockdata);
                    }
                );
            }
        };
    });