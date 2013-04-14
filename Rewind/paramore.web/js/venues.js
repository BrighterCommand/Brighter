$(function () {
    // MOCK: Get the real data from a REST endpoint - this is test data captured from the enpoint by Fiddler
    //[{"address":"Street : StreetNumber: 1, Street: MyStreet, City : London, PostCode : N1 3GA",
    //"contact":"ContactName: Ian, EmailAddress: ian@huddle.com, PhoneNumber: 123454678",
    //"links":[{"HRef":"\/\/localhost:59280\/venue\/5557e160-0f5a-472a-8dab-56f4e70ed15f","Rel":"self"},
    //{"HRef":"http:\/\/www.mysite.com\/maps\/12345","Rel":"map"}],
    //"name":"Test Venue",
    //"version":1}]

    //The model
    var model = {
        "address": "Street : StreetNumber: 1, Street: MyStreet, City : London, PostCode : N1 3GA",
        "contact": "ContactName: Ian, EmailAddress: ian@huddle.com, PhoneNumber: 123454678",
        "links": [
            { "HRef": "\/\/localhost:59280\/venue\/5557e160-0f5a-472a-8dab-56f4e70ed15f", "Rel": "self" },
            { "HRef": "http:\/\/www.mysite.com\/maps\/12345", "Rel": "map" }],
        "name": "Test Venue",
        "version": "1"
    };

    //The viewmodel
    var viewModel = function () {
        this.address = ko.observable(model.address);
        this.contact = ko.observable(model.contact);
        this.self = ko.observable(model.links[0]);
        this.map = ko.observable(model.links[1]);
        this.name = ko.observable(model.name);
        this.version = ko.observable(model.version);

    };

    //Note: Data bind the values between the source and the targets using Knockout
    ko.applyBindings(new viewModel());
});