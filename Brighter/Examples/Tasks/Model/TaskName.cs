using System;

namespace Tasks.Model
{
    public class TaskName : IEquatable<TaskName>
    {
        private readonly string taskName;

        public string Value { get { return taskName; } }
        
        public TaskName(string taskName)
        {
            this.taskName = taskName;
        }

        public static implicit operator string(TaskName rhs)
        {
            return rhs.Value;
        }

        public override string ToString()
        {
            return taskName;
        }

        public bool Equals(TaskName other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(taskName, other.taskName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TaskName) obj);
        }

        public override int GetHashCode()
        {
            return taskName.GetHashCode();
        }

        public static bool operator ==(TaskName left, TaskName right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TaskName left, TaskName right)
        {
            return !Equals(left, right);
        }
    }
}