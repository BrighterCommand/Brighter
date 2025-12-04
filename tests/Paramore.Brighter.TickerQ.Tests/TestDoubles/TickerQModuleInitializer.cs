using TickerQ.Utilities;

namespace Paramore.Brighter.TickerQ.Tests.TestDoubles
{
    /// <summary>
    /// this class ensures that TickerQ function providers are built only once during the test run
    /// </summary>
    public static class TickerQModuleInitializer
    {
        private static bool _hasRun = false;
        private static readonly Lock _lockObject = new();
        public static void EnsureOneTimeSetupTickerQ()
        {
            lock (_lockObject)
            {
                if (!_hasRun)
                {
                    TickerFunctionProvider.Build(); 
                    _hasRun = true;
                }
            }
        }
    }
}
