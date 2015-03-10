var listVm = function () {
    var taskList;
    var getCallback, postCallback;
    var getTasks = function () {
        $.ajax({
            url: "/tasklist",
            dataType: 'json',
            type: 'GET',
            success: function(data) {
                taskList = data;
                getCallback(taskList);
            }
        });
    };
    var addTaskInternal = function (taskText, addCallback) {
        postCallback = addCallback;
        //TODO: sort the date format.
        //TODO: get this working (!)
        $.ajax({
            url: "/tasklist/create",
            dataType: 'json',
            contentType: "application/json",
            type: 'POST',
            data: "{'dueDate':'01-Jan-2014', 'taskDescription':'" + taskText + "', 'taskName':'" + taskText + "'}",
            success: function (data) {
                alert('added success:' + data);
                postCallback(data);
            },
            error: function (jqXhr, textStatus, errorThrown) {
                postCallback(jqXhr.responseText);
            }
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
    //alert(tl);
    var content = Mustache.to_html($("#viewTemplate").html(), tl);
    $("#taskContainer").html(content);
}
var onTaskCreated = function (newHref) {
    alert('added! ' + newHref);
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

