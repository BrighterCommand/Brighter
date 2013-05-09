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
                var request = requests[i];
                amplify.request.define(
                    request.resourceId,
                    function (resource) {
                        resource.success({
                            "address": { "streetNumber": "123", "street": "Sesame Street", "city": "New York", "postCode": "10128" },
                            "contact": { "name": "Elmo", "emailAddress": "elmo@bigbird.com", "phoneNumber": "123454678" },
                            "links": [
                                { "HRef": "\/\/localhost:59280\/venue\/5557e160-0f5a-472a-8dab-56f4e70ed15f", "Rel": "self" },
                                { "HRef": "http:\/\/goo.gl\/maps\/vN5Gk", "Rel": "map" }],
                            "name": "Hooper's Store",
                            "version": "1"
                        });
                    }
                );
            }
        };
    });