define(['durandal/system'], function (system) {

    var addVenueModal = {
        activate: activate,
        response: response,
        onbtnSubmit: onbtnSubmit,
        onbtnCancel: onbtnCancel
    };

    function activate () {
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