var logger  = function (logLine) {
    //var currentdate = new Date();
    //var missString = currentdate.getMinutes() + ":" 
    //        + currentdate.getSeconds() + "."
    //        + currentdate.getMilliseconds();
    //console.log(missString + " " + logLine);
    //$("#logLine").append(missString + " " + logLine + "<br />");
}

$(document).ready(function () {
    var baseUriDef = 'http://localhost:3579/';
    messagesVm.init(baseUriDef, logger);
    
    storesVm.init(baseUriDef, messagesVm, logger);
    storesVm.load();
});

