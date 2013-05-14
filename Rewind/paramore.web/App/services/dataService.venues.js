// MOCK: Get the real data from a REST endpoint - this is test data captured from the enpoint by Fiddler
//[{
//    "address": { "city": "", "postCode": "", "street": "", "streetnumber": "" },
//    "contact": { "emailAddress": "ian@huddle.com", "name": "Ian", "phoneNumber": "123454678" },
//    "links": [{ "HRef": "\/\/localhost:59280\/venue\/8b8c66fc-d541-4051-94ed-1699209d69b0", "Rel": "self" },
//      { "HRef": "http:\/\/www.mysite.com\/maps\/12345", "Rel": "map" }],
//    "name": "Test Venue",
//    "version": 1
//}]

define(['durandal/system'],  function (system) {

    var getVenues = function() {
        var venues = [];
        //set ajax call
        var options = {
            url: "http://localhost:31290/venues",
            cache: false,
            type: 'GET',
            dataType: 'JSON'
        };


        //make call
        return system.defer(function(dfd) {
            amplify.request({
                resourceId: 'venues',
                success: dfd.resolve,
                error: dfd.reject
            });
        }).promise();
    };
    

    var dataservice = {
        getVenues : getVenues
    };

    return dataservice;
});