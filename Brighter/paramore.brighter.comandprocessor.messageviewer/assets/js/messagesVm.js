var messagesVm = function () {
    var pageSize = 100;
    var baseUri;
    var seletedStoreName;
    var selectedPageNumber = 1;
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

        $("#pagePrevious").click(onPagePrevious);
        $("#pageNext").click(onPageNext);

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
                error: function(jqXhr, textStatus, errorThrown) {
                    onMessageLoadError(textStatus, errorThrown);
                }
            });
        }
    };
    var onSearchClearClick = function () {
        log("click clear");
        $('#messageSearchText').val("");
        selectedPageNumber = 1;
        loadFirstPageInternal(seletedStoreName);
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
    var loadFirstPageInternal = function (storeName) {
        loadInternal(storeName, 1);
    }
    var loadInternal = function (storeName, pageNumber) {
        seletedStoreName = storeName;
        if (storeName) {
            showMessageSpinner();
            deactivateSearch();
            log("start load");
            $.ajax({
                url: baseUri + "/messages/" + storeName + "/" + pageNumber,
                dataType: 'json',
                type: 'GET',
                success: function(data) { onMessageLoad(data); },
                error: function(jqXhr, textStatus, errorThrown) {
                    onMessageLoadError(jqXhr, textStatus, errorThrown);
                }
            });
        }
    };
    var onPageNext = function () {
        selectedPageNumber++;
        loadInternal(seletedStoreName, selectedPageNumber);
        return false;
    }
    var onPagePrevious = function() {
        selectedPageNumber--;
        loadInternal(seletedStoreName, selectedPageNumber);
        return false;
    }
    var onMessageLoad = function (messageModel) {
        sVm.setGoodServer(seletedStoreName);

        hideMessageSpinner();
        $("#messageContainer").show();
        activateSearch();
        model = messageModel;
        var content = Mustache.to_html($("#messageTemplate").html(), messageModel);
        $("#messageList").html(content);

        if (messageModel.MessageCount < pageSize) {
            $("#pageControls").hide();
        } else {
            $("#pageControls").show();
        }
        if (selectedPageNumber > 1) {
            $("#pageControlsPrevious").show();
        } else {
            $("#pageControlsPrevious").hide();
        }
        $("#pageNumber").text(selectedPageNumber);

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
    function isJsonString(str) {
        try {
            JSON.parse(str);
        } catch (e) {
            return false;
        }
        return true;
    }
    var onMessageLoadError = function (jqXhr, status, error) {
        var responseText = jqXhr.responseText;
        if (isJsonString(responseText)) {
            error = JSON.parse(responseText).Message;
        }
        hideInternal();
        setBadServer(seletedStoreName, error);
    };
    var setStoreVmInternal = function(storeVm) {
        sVm = storeVm;
    };
    return {
        loadFirstPage: loadFirstPageInternal,
        init: initInternal,
        hide: hideInternal,
        setStoresRef: setStoreVmInternal
    };
}();
