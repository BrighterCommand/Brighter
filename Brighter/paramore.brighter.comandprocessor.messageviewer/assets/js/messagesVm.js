// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 25-03-2014
//
// Last Modified By : ian
// Last Modified On : 25-03-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

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
        $("#messageRepost").click(onMessageRepost);

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
    var repostInternal = function (storeName, messageList) {
        seletedStoreName = storeName;
        if (storeName) {
            showMessageSpinner();
            deactivateSearch();
            log("start repost");
            $.ajax({
                url: baseUri + "/messages/" + storeName + "/repost/" + messageList,
                dataType: 'json',
                type: 'POST',
                data: messageList,
                success: function (data) { loadFirstPageInternal(seletedStoreName); },
                error: function (jqXhr, textStatus, errorThrown) {
                    onMessageLoadError(jqXhr, textStatus, errorThrown);
                }
            });
        }
    };

    var onMessageRepost = function () {
        repostInternal(seletedStoreName, $("#messagesSelectedIds").text());
    }
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
        $("#messageActionPanel button").addClass("disabled");
        activateSearch();
        model = messageModel;
        var content = Mustache.to_html($("#messageTemplate").html(), messageModel);
        $("#messageList").html(content);

        if (selectedPageNumber > 1) {
            $("#pagePrevious").removeClass("disabled");
        } else {
            $("#pagePrevious").addClass("disabled");
        }
        if (messageModel.messageCount <= pageSize && messageModel.messageCount >0) {
            $("#pageNext").removeClass("disabled");
        } else {
            $("#pageNext").addClass("disabled");
        }

        $("#messagesSelectedNumber").text("");
        $("#messagesSelectedIds").text("");
        $("#pageNumber").text(selectedPageNumber);
        $("#messageContainer td input.messageCheck").change(onMessageChecked);
        log("end load");
    };
    var onMessageChecked = function() {
        var checkedMessages = $("#messageContainer td input.messageCheck").filter(":checked");
        $("#messagesSelectedNumber").text(checkedMessages.length);

        var messageIds = [];
        checkedMessages.each(function (index,element) {
            messageIds.push($(element).parents("tr").find(".messageId").text());
        });
        $("#messagesSelectedIds").text(messageIds.toString());

        if (checkedMessages.length > 0) {
            $("#messageActionPanel button").removeClass("disabled");
        } else {
            $("#messageActionPanel button").addClass("disabled");
        }
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
