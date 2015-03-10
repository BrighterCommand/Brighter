var listVm = function () {
    var taskList;
    var getCallback, addCallback;
    var getTasks = function () {
        $.ajax({
            //url: "/tasklist",
            url: 'http://localhost:49743/tasks',
            dataType: 'json',
            type: 'GET',
            success: function(data) {
                taskList = data;
                getCallback(taskList);
            }
        });
    };
    var addTaskInternal = function (taskText, addCallback) {
        addCallback = addCallback;
        //TODO: sort the date format.
        $.ajax({
//            url: 'http://localhost:49743/tasks',
//            dataType: 'json',

            url: "/tasklist/create",
            dataType: 'text',
            contentType: "application/json",
            type: 'POST',
            data: "{'dueDate':'01-Jan-2014', 'taskDescription':'" + taskText + "', 'taskName':'" + taskText + "'}",
            success: function (data) {
                addCallback(data);
            },
        });
    };
    var completeTaskInternal = function (taskText, completeCallback) {
        addCallback = addCallback;
        //TODO: sort the date format.
        $.ajax({
            url: "/tasklist/create",
            //dataType: 'text',
            type: 'DELETE',
            data: "{'dueDate':'01-Jan-2014', 'taskDescription':'" + taskText + "', 'taskName':'" + taskText + "'}",
            success: function (data) {
                addCallback(data);
            },
            error: function (data) {
                addCallback(data);
            },
        });
    };
    return {
        init: function (cb) {
            getCallback = cb;
            getTasks();
        },
        addTask: addTaskInternal
    };
}();

var onTaskLoad = function (tl) {
    var content = Mustache.to_html($("#viewTemplate").html(), tl);
    $("#taskContainer").html(content);
}
var onTaskCreated = function (newHref) {
    //alert('added! ' + newHref);
    listVm.init(onTaskLoad);
}
var onTaskAddClick = function () {
    var parent = $("#todoAdd").parent();
    var taskText = parent.children().first().val();
    listVm.addTask(taskText, onTaskCreated);
}
$(document).ready(function () {
    listVm.init(onTaskLoad);
    $("#todoAdd").click(onTaskAddClick);
    //listVm.addTask(toDo.create("testTask"));
    //listVm.addTask(toDo.create("testTask"));
    //listVm.addTask(toDo.create("testTask"));
});

