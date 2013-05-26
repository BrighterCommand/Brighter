define(['durandal/system'], function (system) {

    var response = {
        closedWith: 'cancel',
        name: ko.observable(),
        buildingNumber: ko.observable(),
        streetName: ko.observable(),
        city: ko.observable(),
        postcode: ko.observable(),
        contactName: ko.observable(),
        emailAddress: ko.observable(),
        phoneNumber: ko.observable(),
        map: ko.observable(),
        self: ko.observable(),
        version: ko.observable()
    };

    var addVenueModal = {
        activate: activate,
        response: response,
        onbtnSubmit: onbtnSubmit,
        onbtnCancel: onbtnCancel
    };

    function activate() {
        system.log("Activating Add Venue Modal");
    };

     function onbtnSubmit(dialogResult) {
        this.response.closedWith = 'ok';
        this.modal.close(this.response);
    };

     function onbtnCancel() {
        this.response.closedWith = 'cancel';
        this.modal.close();
    };

    return addVenueModal;
});