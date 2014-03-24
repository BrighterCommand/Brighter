using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace paramore.brighter.commandprocessor
{
    internal static class ReflectionExtensions
    {
        internal static IEnumerable<RequestHandlerAttribute> GetOtherHandlersInPipeline(this MethodInfo targetMethod)
        {
            var customAttributes = targetMethod.GetCustomAttributes(true);
            return customAttributes
                     .Select(attr => (Attribute)attr)
                     .Cast<RequestHandlerAttribute>()
                     .Where(a => a.GetType().BaseType == typeof(RequestHandlerAttribute))
                     .ToList();
        }
    }

}
