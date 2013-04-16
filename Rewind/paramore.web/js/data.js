var paramore = paramore || {}; //paramore namespace

// MOCK: Get the real data from a REST endpoint - this is test data captured from the enpoint by Fiddler
//[{"address":"Street : StreetNumber: 1, Street: MyStreet, City : London, PostCode : N1 3GA",
//"contact":"ContactName: Ian, EmailAddress: ian@huddle.com, PhoneNumber: 123454678",
//"links":[{"HRef":"\/\/localhost:59280\/venue\/5557e160-0f5a-472a-8dab-56f4e70ed15f","Rel":"self"},
//{"HRef":"http:\/\/www.mysite.com\/maps\/12345","Rel":"map"}],
//"name":"Test Venue",
//"version":1}]

//The model
paramore.model = function () {
    var data = {
        Venues: [
            {
                "address": "Street : StreetNumber: 100, Street: City Road, City : London, PostCode : EC1Y 2BP",
                "contact": "ContactName: Ian, EmailAddress: ian@huddle.com, PhoneNumber: 123454678",
                "links": [
                    { "HRef": "\/\/localhost:59280\/venue\/5557e160-0f5a-472a-8dab-56f4e70ed15f", "Rel": "self" },
                    { "HRef": "http:\/\/goo.gl\/maps\/OX8n0", "Rel": "map" }],
                "name": "Huddle",
                "version": "1"
            },
            {
                "address": "Street : StreetNumber: 123, Street: Sesame Street, City : New York, PostCode : 10128",
                "contact": "ContactName: Elmo, EmailAddress: elmo@bigbird.com, PhoneNumber: 123454678",
                "links": [
                    { "HRef": "\/\/localhost:59280\/venue\/5557e160-0f5a-472a-8dab-56f4e70ed15f", "Rel": "self" },
                    { "HRef": "http:\/\/goo.gl\/maps\/vN5Gk", "Rel": "map" }],
                "name": "Hooper's Store",
                "version": "1"
            }
        ]   
    };
    

    return {
        data: data
    };
}();