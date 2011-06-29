using System;
using UserGroupManagement.ServiceLayer.CommandHandlers;

namespace UserGroupManagement.ServiceLayer.CommandProcessor
{
    public abstract class RequestHandlerRequiredDecoratorAttribute : Attribute
    {
        public abstract Type GetDecoratorType();
    }
}