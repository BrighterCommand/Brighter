var listVm = function () {
    var taskList;
    var getCallback, addCallback, completeCallback;
    var getTasks = function () {
        $.ajax({
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
            dataType: 'text', //to process location, not json
            url: 'http://localhost:49743/tasks',
            contentType: "application/json",
            type: 'POST',
            data: '{"dueDate": "01-Jan-2014", "taskDescription": "' + taskText + '", "taskName": "' + taskText + '"}',
            success: function (data) {
                addCallback(data);
            },
        });
    };
    var completeTaskInternal = function (taskId, completeCb) {
        completeCallback = completeCb;
        $.ajax({
            url: 'http://localhost:49743/tasks/' + taskId,
            dataType: 'json',
            type: 'DELETE',
            success: function (data) {
                addCallback(data);
            }
        });
    };
    return {
        init: function (cb) {
            getCallback = cb;
            getTasks();
        },
        addTask: addTaskInternal,
        completeTask: completeTaskInternal
    };
}();
var refreshTaskList = function() {
    listVm.init(onTaskLoad);
}
var onTaskCompleteClick = function() {
    //TODO - get the right Id here!!!!
    var taskUri= $(this).parent().find(".taskHref").text();
    var taskId = taskUri.substring(taskUri.lastIndexOf('/') + 1);
    listVm.completeTask(taskId, onTaskCompletedCb);
}
var onTaskCompletedCb = function() {
    refreshTaskList();
}
var onTaskLoad = function (tl) {
    var content = Mustache.to_html($("#viewTemplate").html(), tl);
    $("#taskContainer").html(content);
    //bind complete
    $("#taskContainer").find(".complete").click(onTaskCompleteClick);
}
var onTaskCreated = function (newHref) {
    refreshTaskList();
}
var onTaskAddClick = function () {
    var parent = $("#todoAdd").parent();
    var taskText = parent.children().first().val();
    listVm.addTask(taskText, onTaskCreated);
}
$(document).ready(function () {
    refreshTaskList();
    $("#todoAdd").click(onTaskAddClick);
    //listVm.addTask(toDo.create("testTask"));
    //listVm.addTask(toDo.create("testTask"));
    //listVm.addTask(toDo.create("testTask"));
});

