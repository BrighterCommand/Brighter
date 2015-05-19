var messagesVm = function () {
    var baseUri;
    var seletedStoreName;
    var model;
    var log;
    var sVm;

    var initInternal = function(baseUriDep, logger) {
        baseUri = baseUriDep;
        log = logger;
        $("#messageSearchButton").click(onSearchClick);
        $("#messageSearchText").keyup(function(e) {
            if (e.keyCode == 13) {
                onSearchClick();
            }
        });
        $("#messageClearButton").click(onSearchClearClick);
        $("#messageContainer").hide();
        hideMessageSpinner();
    };
    var onSearchClick = function() {
        log("click search");
        var searchValue = $("#messageSearchText").val();
        if (searchValue) {
            showMessageSpinner();
            deactivateSearch();
            log("start search");
            $.ajax({
                url: baseUri + "/stores/search/" + seletedStoreName + "/" + searchValue,
                dataType: 'json',
                type: 'GET',
                success: function(data) { onSearchMessageLoad(data); },
                error: function(jqXHR, textStatus, errorThrown) {
                    onMessageLoadError(textStatus, errorThrown);
                }
            });
        }
    };
    var onSearchClearClick = function () {
        log("click clear");
        $('#messageSearchText').val("");
        loadInternal(seletedStoreName);
    };

    var deactivateSearch = function () {
        $('#messageSearchButton').attr("disabled", true);
         $('#messageClearButton').attr("disabled", "true");
    };
    var activateSearch = function () {
        $('#messageSearchButton').attr("disabled", false);
        $('#messageClearButton').attr("disabled", false);
    };
    
    var showMessageSpinner = function () {
    };
    var hideMessageSpinner = function() {
    }
    var loadInternal = function(storeName) {
        seletedStoreName = storeName;
        if (storeName) {
            showMessageSpinner();
            deactivateSearch();
            log("start load");
            $.ajax({
                url: baseUri + "/messages/" + storeName,
                dataType: 'json',
                type: 'GET',
                success: function(data) { onMessageLoad(data); },
                error: function(jqXHR, textStatus, errorThrown) {
                    onMessageLoadError(jqXHR, textStatus, errorThrown);
                }
            });
        }
    };

    var onMessageLoad = function (messageModel) {
        sVm.setGoodServer(seletedStoreName);

        hideMessageSpinner();
        $("#messageContainer").show();
        activateSearch();
        model = messageModel;
        var content = Mustache.to_html($("#messageTemplate").html(), messageModel);
        $("#messageList").html(content);
        log("end load");
    };

    var onSearchMessageLoad = function(messageModel) {
        onMessageLoad(messageModel);
        log("end search load");

    };
    var hideInternal = function() {
        $("#messageContainer").hide();
    };
    var setBadServer = function (storeName, error) {
        if (sVm) {
            sVm.setBadServer(storeName, error);
        }
    };

    var onMessageLoadError = function (jqXHR, status, error) {
        hideInternal();
        setBadServer(seletedStoreName, error);
    };
    var setStoreVmInternal = function(storeVm) {
        sVm = storesVm;
    };
    return {
        load: loadInternal,
        init: initInternal,
        hide: hideInternal,
        setStoresRef: setStoreVmInternal
    };
}();
