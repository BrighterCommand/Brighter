var storesVm = function () {
    var baseUri;
    var model, messageModel;
    var seletedStoreName;
    var log;

    var initInternal = function(baseUriDep, mVm, logger) {
        messageModel = mVm;
        mVm.setStoresRef(storesVm);
        baseUri = baseUriDep;
        log = logger;
    };
    var loadInternal = function() {
        log("about to load stores ");

        $.ajax({
            url: baseUri + "/stores",
            dataType: 'json',
            type: 'GET',
            success: function(data) { onStoreLoad(data); },
            error: function(jqXHR, textStatus, errorThrown) {
                onStoreLoadError(jqXHR, textStatus, errorThrown);
            }
        });

    };
    var getInternal = function() {
        return model;
    };

    var selectStore = function(storeName) {
        log("selected " + storeName);
        seletedStoreName = storeName;
        messageModel.load(storeName);
        log("doneSelecting " + storeName);
    };
    var onStoreLoad = function(storesModel) {
        log("got stores Loaded");
        model = storesModel;
        var content = Mustache.to_html($("#storeTemplate").html(), storesModel);
        $("#storeList").html(content);

        var listItems = "";
        for (var i = 0; i < model.Stores.length; i++) {
            listItems += "<option value='" + model.Stores[i].Name + "'>"
                + model.Stores[i].Name + "</option>";
        };
        $('#storeList').on('hidden.bs.collapse', function(e) {
            var storeId = e.target.id;
        });
        $('#storeList').on('shown.bs.collapse', function(e) {
            var storeId = e.target.id;
            //firstoccuranceonly
            selectStore(storeId.replace("store", ""));
        });
        log("done stores Loaded");

    };
    var onStoreLoadError = function (jqXHR, status, error) {
        // alert(error);
        setBadServerInternal(seletedStoreName, error);
    };
    var setGoodServerInternal = function(storeName) {
        var status = $("#storeStatus" + storeName);
        status
            .removeClass("label-default")
            .removeClass("label-danger")
            .addClass("label-success");
        status.text("good");
    };
    var setBadServerInternal = function (storeName, error) {
        var status = $("#storeStatus" + storeName);
        if (status) {
            status
                .removeClass("label-default")
                .removeClass("label-success")
                .addClass("label-danger");
            status.text("error - " + error);
        }
        else {
            console.log("Cannot find UI element for store " + storeName + " with error " + error);
        }
    };
    return {
        load: loadInternal,
        get: getInternal,
        init: initInternal,
        setGoodServer: setGoodServerInternal,
        setBadServer: setBadServerInternal,
    };
}();
