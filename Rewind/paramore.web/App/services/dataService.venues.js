define(['durandal/system'],  function (system) {

    var getVenues = function() {
        //make call
        return system.defer(function(dfd) {
            amplify.request({
                resourceId: 'venues',
                success: dfd.resolve,
                error: dfd.reject
            });
        }).promise();
    };

    var addVenue = function(newVenue) {
        //make call
        return system.defer(function(dfd) {
            amplify.request({
                resourceId: 'addVenue',
                data: JSON.stringify(newVenue),
                success: dfd.resolve,
                error: dfd.reject,
                dataMap: function(data) { 
                    if(typeof data === 'object') { 
                        return JSON.stringify(data); 
                    } 
                    return data; 
                } 
            });
        }).promise();
    };
    

    var dataservice = {
        addVenue : addVenue,
        getVenues : getVenues
    };

    return dataservice;
});