var taskVm = function () {
    var baseUri = 'http://localhost:49743/tasks';
    var dueDateFixed = '01-Jan-2014';
    var getTasksInternal = function(getCallback) {
        $.ajax({
            url: baseUri,
            dataType: 'json',
            type: 'GET',
            success: function(data) { getCallback(data); }
        });
    };
    var addTaskInternal = function(taskText, addCallback) {
        //TODO: change FIXED due date, and desc==name
        $.ajax({
            url: baseUri,
            dataType: 'text', //to process location, not json
            type: 'POST',
            success: function(data) { addCallback(data); },
            contentType: "application/json",
            data: '{"dueDate": "' + dueDateFixed + '", "taskDescription": "' + taskText + '", "taskName": "' + taskText + '"}'
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
    var sendReminderInternal = function (dueDate, taskName, recipient, reminderCb) {
        $.ajax({
            url: baseUri + '/reminders',
            dataType: 'text', //to process location, not json
            type: 'POST',
            success: function (data) { reminderCb(data); },
            contentType: "application/json",
            data: '{"dueDate": "' + dueDate + '", "recipient": "' + recipient + '", "copyTo": "' + recipient + '", "taskName": "' + taskName + '"}'
        });
    }
    return {
        getTasks: getTasksInternal,
        addTask: addTaskInternal,
        completeTask: completeTaskInternal,
        sendReminder: sendReminderInternal
    };
}();
var refreshTaskList = function() {
    taskVm.getTasks(onTaskLoad);
};
var onTaskCompleteClick = function() {
    var taskUri = $(this).parent().find(".taskHref").text();
    var taskId = taskUri.substring(taskUri.lastIndexOf('/') + 1);
    taskVm.completeTask(taskId, onTaskCompletedCb);
};
var onTaskCompletedCb = function(data) {
    refreshTaskList();
};
var onReminderSentCb = function(data) {
    //alert('sent it!');
    closeMailFormsAndShowReminderButtons();
}
var onReminderSendClick = function () {
    var divForm = $(this).parent();
    var dueDate = divForm.find("input[name=dueDate]").val();
    var taskName = divForm.find("input[name=taskName]").val();
    var recipient = divForm.find("input[name=recipient]").val();

    taskVm.sendReminder(dueDate, taskName, recipient, onReminderSentCb);
}
var onTaskLoad = function(tl) {
    tl.items.sort(taskSorter);
    var content = Mustache.to_html($("#viewTemplate").html(), tl);
    $("#taskContainer").html(content);
    bindTaskEvents();
};
var onTaskCreatedCb = function(newHref) {
    refreshTaskList();
    $("#taskAddName").val("");
};
var onTaskAddClick = function() {
    var parent = $("#taskAddName").parent();
    var taskText = parent.children().first().val();
    taskVm.addTask(taskText, onTaskCreatedCb);
};
$(document).ready(function() {
    refreshTaskList();
    $("#taskAddName").bind('blur keyup', function (e) {
        if (e.type === 'keyup' && e.keyCode !== 10 && e.keyCode !== 13) return;
        onTaskAddClick();
    });
});
var onReminderClick = function () {
    $(this).hide();
    closeMailFormsAndShowReminderButtons();
    var thisForm = $(this).parents("li").find("div.mailForm");
    thisForm.css({ 'display': 'block' });
    thisForm.find("button").click(onReminderSendClick);
}
var bindTaskEvents = function () {
    $("#taskContainer")
        .find(".complete")
        .click(onTaskCompleteClick);
    $("#taskList button.mail").bind('click', onReminderClick);
}
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
var closeMailFormsAndShowReminderButtons = function () {
    $("#taskList li div.mailForm").css({ 'display': 'none' });
    $("#taskList button.mail").show();
}

