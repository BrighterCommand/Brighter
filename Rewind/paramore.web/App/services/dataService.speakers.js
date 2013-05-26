define(['durandal/system'],  function (system) {

    var getSpeakers = function() {
        //make call
        return system.defer(function(dfd) {
            amplify.request({
                resourceId: 'speakers',
                success: dfd.resolve,
                error: dfd.reject
            });
        }).promise();
    };
    

    var dataservice = {
        getSpeakers : getSpeakers
    };

    return dataservice;
});