using System;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Tests.Logging.TestDoubles
{
    internal class SpyLogProvider : ILogProvider
    {
        private readonly SpyLog _logger;

        public SpyLogProvider(SpyLog logger)
        {
            _logger = logger;
        }

        public Logger GetLogger(string name)
        {
            return (logLevel, messageFunc, exception, formatParameters) => _logger.Log(logLevel, messageFunc, exception, formatParameters);
        }

        public IDisposable OpenNestedContext(string message)
        {
            throw new NotImplementedException();
        }

        public IDisposable OpenMappedContext(string key, string value)
        {
            throw new NotImplementedException();
        }
    }
}