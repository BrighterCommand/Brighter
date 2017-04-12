using System;
using System.Collections.Generic;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Tests.Logging.TestDoubles
{
    internal class SpyLog : ILog
    {
        public IList<LogRecord> Logs { get; } = new List<LogRecord>();

        public bool Log(LogLevel logLevel, Func<string> messageFunc, Exception exception = null, params object[] formatParameters)
        {
            //if we are checking if level is supported
            if (messageFunc == null)
                return true;

            Logs.Add(new LogRecord(logLevel, string.Format(messageFunc(), formatParameters), exception));

            return true;
        }

        internal class LogRecord
        {
            public LogRecord(LogLevel logLevel, string message, Exception exception)
            {
                LogLevel = logLevel;
                Message = message;
                Exception = exception;
            }

            public LogLevel LogLevel { get; }
            public string Message { get; }
            public Exception Exception { get; }
        }
    }
}
