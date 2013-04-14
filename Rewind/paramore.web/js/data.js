var paramore = paramore || {}; //paramore namespace

// MOCK: Get the real data from a REST endpoint - this is test data captured from the enpoint by Fiddler
//[{"address":"Street : StreetNumber: 1, Street: MyStreet, City : London, PostCode : N1 3GA",
//"contact":"ContactName: Ian, EmailAddress: ian@huddle.com, PhoneNumber: 123454678",
//"links":[{"HRef":"\/\/localhost:59280\/venue\/5557e160-0f5a-472a-8dab-56f4e70ed15f","Rel":"self"},
//{"HRef":"http:\/\/www.mysite.com\/maps\/12345","Rel":"map"}],
//"name":"Test Venue",
//"version":1}]

//The model
paramore.model = {
    "address": "Street : StreetNumber: 1, Street: MyStreet, City : London, PostCode : N1 3GA",
    "contact": "ContactName: Ian, EmailAddress: ian@huddle.com, PhoneNumber: 123454678",
    "links": [
        { "HRef": "\/\/localhost:59280\/venue\/5557e160-0f5a-472a-8dab-56f4e70ed15f", "Rel": "self" },
        { "HRef": "http:\/\/www.mysite.com\/maps\/12345", "Rel": "map" }],
    "name": "Test Venue",
    "version": "1"
};