using System;
using System.Collections.Generic;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles
{
    internal class FakeLogProvider : ILogProvider
    {
        public static List<string> LoggedMessages = new List<string>();

        private static bool GetLogger(LogLevel logLevel, Func<string> messageFunc, Exception exception, params object[] formatParameters)
        {
            if (messageFunc != null)
            {
                LoggedMessages.Add(messageFunc.Invoke());                
            }
            return true;
        }

        public Logger GetLogger(string name)
        {
            return GetLogger;
        }

        public IDisposable OpenNestedContext(string message)
        {
            return null;
        }

        public IDisposable OpenMappedContext(string key, string value)
        {
            return null;
        }
    }
}