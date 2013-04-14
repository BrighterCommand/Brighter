$(function () {
 
    //The viewmodel
    paramore.ViewModel = function () {
        this.address = ko.observable(paramore.model.address);
        this.contact = ko.observable(paramore.model.contact);
        this.self = ko.observable(paramore.model.links[0]);
        this.map = ko.observable(paramore.model.links[1]);
        this.name = ko.observable(paramore.model.name);
        this.version = ko.observable(paramore.model.version);

    };

    //Note: Data bind the values between the source and the targets using Knockout
    ko.applyBindings(new paramore.ViewModel());
});