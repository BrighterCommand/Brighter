var listVm = function () {
    var getTasks = function (getCallback) {
        $.ajax({
            url: 'http://localhost:49743/tasks',
            dataType: 'json',
            type: 'GET',
            success: function(data) {getCallback(data);}
        });
    };
    var addTaskInternal = function (taskText, addCallback) {
        //TODO: sort the date format.
        $.ajax({
            dataType: 'text', //to process location, not json
            url: 'http://localhost:49743/tasks',
            contentType: "application/json",
            type: 'POST',
            data: '{"dueDate": "01-Jan-2014", "taskDescription": "' + taskText + '", "taskName": "' + taskText + '"}',
            success: function (data) {addCallback(data);}
        });
    };
    var completeTaskInternal = function (taskId, completeCb) {
        $.ajax({
            url: 'http://localhost:49743/tasks/' + taskId,
            dataType: 'text',
            type: 'DELETE',
            success: function (data) { completeCb(data); }
        });
    };
    return {
        init: getTasks,
        addTask: addTaskInternal,
        completeTask: completeTaskInternal
    };
}();
var refreshTaskList = function() {
    listVm.init(onTaskLoad);
}
var onTaskCompleteClick = function() {
    var taskUri= $(this).parent().find(".taskHref").text();
    var taskId = taskUri.substring(taskUri.lastIndexOf('/') + 1);
    listVm.completeTask(taskId, onTaskCompletedCb);
}
var onTaskCompletedCb = function(data) {
    refreshTaskList();
}
var onTaskLoad = function (tl) {
    var content = Mustache.to_html($("#viewTemplate").html(), tl);
    $("#taskContainer").html(content);
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
});

