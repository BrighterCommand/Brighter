namespace GenericListener.Ports.Events
{
    public class GenericTask : EventStoredEvent { }
    public class GenericTaskAddedEvent : GenericTask { }
    public class GenericTaskEditedEvent : GenericTask { }
    public class GenericTaskCompletedEvent : GenericTask { }
}