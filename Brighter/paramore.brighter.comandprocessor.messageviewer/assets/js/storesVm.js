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
            error: function(jqXhr, textStatus, errorThrown) {
                onStoreLoadError(jqXhr, textStatus, errorThrown);
            }
        });

    };
    var getInternal = function() {
        return model;
    };

    var selectStore = function(storeName) {
        log("selected " + storeName);
        seletedStoreName = storeName;
        messageModel.loadFirstPage(storeName);
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
            //var storeId = e.target.id;

        });
        $('#storeList').on('shown.bs.collapse', function(e) {
            var storeId = e.target.id;
            //firstoccuranceonly
            selectStore(storeId.replace("store", ""));
        });
        log("done stores Loaded");

    };
    var onStoreLoadError = function (jqXhr, status, error) {
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
