using System;

namespace UserGroupManagement.ServiceLayer.CommandProcessor
{
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class RequestHandlerAttribute : Attribute
    {
        private readonly int _step;

        protected RequestHandlerAttribute(int step)
        {
            _step = step;
        }

        public int Step
        {
            get { return _step; }
        }

        public abstract Type GetHandlerType();
    }
}