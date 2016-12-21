using System;
using System.Collections.Generic;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.tests.nunit.Logging.TestDoubles
{
    // todo: fix ILog tests
    //class SpyLog : ILog
    //{
    //    readonly IList<LogRecord> _logs = new List<LogRecord>();

    //    public IEnumerable<LogRecord> Logs { get { return _logs; } } 

    //    public bool Log(LogLevel logLevel, Func<string> messageFunc, Exception exception = null, params object[] formatParameters)
    //    {
    //        //if we are checking if level is supported
    //        if (messageFunc == null)
    //        {
    //            return true;
    //        }

    //        var record = new LogRecord(logLevel, string.Format(messageFunc(), formatParameters), exception);
    //        _logs.Add(record);
    //        return true;
    //    }

    //    internal class LogRecord
    //    {
    //        public LogRecord(LogLevel logLevel, string message, Exception exception)
    //        {
    //            LogLevel = logLevel;
    //            Message = message;
    //            Exception = exception;
    //        }

    //        public LogLevel LogLevel { get; private set; }
    //        public string Message { get; private set; }
    //        public Exception Exception { get; private set; }
    //    }
    //}
}
