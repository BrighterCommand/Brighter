var listVm = function () {
    var baseUri = 'http://localhost:49743/tasks';
    var getTasks = function(getCallback) {
        $.ajax({
            url: baseUri,
            dataType: 'json',
            type: 'GET',
            success: function(data) { getCallback(data); }
        });
    };
    var addTaskInternal = function(taskText, addCallback) {
        //TODO: sort the date format.
        $.ajax({
            url: baseUri,
            dataType: 'text', //to process location, not json
            type: 'POST',
            success: function(data) { addCallback(data); },
            contentType: "application/json",
            data: '{"dueDate": "01-Jan-2014", "taskDescription": "' + taskText + '", "taskName": "' + taskText + '"}'
        });
    };
    var completeTaskInternal = function(taskId, completeCb) {
        $.ajax({
            url: baseUri + '/' + taskId,
            dataType: 'text',
            type: 'DELETE',
            success: function(data) { completeCb(data); }
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
};
var onTaskCompleteClick = function() {
    var taskUri = $(this).parent().find(".taskHref").text();
    var taskId = taskUri.substring(taskUri.lastIndexOf('/') + 1);
    listVm.completeTask(taskId, onTaskCompletedCb);
};
var onTaskCompletedCb = function(data) {
    refreshTaskList();
};
function taskSorter(a, b) {
    //completed last, then by id
    if (a.isComplete && b.isComplete) {
        if (a.completionDate < b.completionDate) {
            return 1;
        }
        return -1;
    }
    if (a.isComplete || b.isComplete) {
        if (a.isComplete) {
            return 1;
        }
        return -1;
    }
    if (a.id < b.id) {
        return 1;
    }
    return -1;
}
var onTaskLoad = function(tl) {
    tl.items.sort(taskSorter);
    var content = Mustache.to_html($("#viewTemplate").html(), tl);
    $("#taskContainer").html(content);
    $("#taskContainer").find(".complete").click(onTaskCompleteClick);
};
var onTaskCreated = function(newHref) {
    refreshTaskList();
    $("#taskAddName").val("");
};
var onTaskAddClick = function() {
    var parent = $("#taskAddName").parent();
    var taskText = parent.children().first().val();
    listVm.addTask(taskText, onTaskCreated);
};
$(document).ready(function() {
    refreshTaskList();
    $("#taskAddName").bind('blur keyup', function (e) {
        if (e.type === 'keyup' && e.keyCode !== 10 && e.keyCode !== 13) return;
        onTaskAddClick();
    });
});