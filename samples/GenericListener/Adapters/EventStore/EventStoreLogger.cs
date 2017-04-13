using System;
using EventStore.ClientAPI;
using log4net;

namespace GenericListener.Adapters.EventStore
{
    public class EventStoreLogger : ILogger
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(EventStoreLogger));

        public void Error(string format, params object[] args)
        {
            _logger.Error(FlattenMessage(format,args));
        }

        public void Error(Exception ex, string format, params object[] args)
        {
            _logger.Error(FlattenMessage(format,args));
        }

        public void Info(string format, params object[] args)
        {
            _logger.Info(FlattenMessage(format,args));
        }

        public void Info(Exception ex, string format, params object[] args)
        {
            _logger.Info(FlattenMessage(format,args));
        }

        public void Debug(string format, params object[] args)
        {
            _logger.Debug(FlattenMessage(format,args));
        }

        public void Debug(Exception ex, string format, params object[] args)
        {
            _logger.Debug(FlattenMessage(format,args));
        }

        private static string FlattenMessage(string format, params object[] args)
        {
            return args.Length > 0 ? string.Format(format,args) : format;
        }
    }
}
