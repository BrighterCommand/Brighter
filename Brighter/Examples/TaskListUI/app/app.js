var listVm = function () {
    var taskList;

    var getTasks = function () {
        //$.cookie('Origin', '*', { path: '/' });
//        $.support.cors = true;

        $.ajax({
            //            url: "http://localhost:49743/tasks",
            url: "/tasklist",
            dataType: 'json',
            type: 'GET',
            crossDomain: true,
            success: function (data) {
                alert("success");
                taskList = data;
            },
            error: function (request, status, ex) {
                alert("error");
            }
        });
    };
    
    var addTaskInternal = function(task) {
        taskList.push(task);
    };

    return {
        init: function() { getTasks(); },
        getTasks: function() { return taskList; },
        addTask: addTaskInternal
    };
}();

var navVm = function () {
    var initialise = function() {
        $("#navViewTasks").on('click', viewTaskClick);
        $("#navAddTask").on('click', addTaskClick);
        $("#navSendTask").on('click', sendTaskClick);
    };

    var viewTaskClick = function (e) {

        //then display result in container
        var templateToBind = $("#addTemplate");
        var vmDataSource = listVm.getTasks();
        var content = Mustache.to_html(templateToBind, vmDataSource);
        $("#contentContainer").html(content);
    };
    var addTaskClick = function (e) {

    };
    var sendTaskClick = function (e) {

    };

    return {
        init: initialise
    };
}();

$(document).ready(function () {
    listVm.init();
    navVm.init();

    //listVm.addTask(toDo.create("testTask"));
    //listVm.addTask(toDo.create("testTask"));
    //listVm.addTask(toDo.create("testTask"));

    //        var mustacheContent = Mustache.to_html(templateCache.cellMenu, documentModel);

});


