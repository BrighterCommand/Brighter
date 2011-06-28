using System;
using UserGroupManagement.ServiceLayer.CommandProcessor;

namespace UserGroupManagement.ServiceLayer
{
    public class ChainofResponsibilityBuilder
    {
        public ChainOfResponsibility Build(Type type)
        {
            return new ChainOfResponsibility();
        }
    }
}