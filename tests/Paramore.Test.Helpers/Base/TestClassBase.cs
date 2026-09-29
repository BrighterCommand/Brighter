using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Test.Helpers.Extensions;
using Paramore.Test.Helpers.TestOutput;

namespace Paramore.Test.Helpers.Base
{
    /// <summary>
    /// Serves as a base class for test classes in the AOT test suite.
    /// This abstract class provides shared setup and utility functionality for derived test classes.
    /// </summary>
    /// <typeparam name="T">
    /// The type parameter representing the specific test class deriving from this base class.
    /// </typeparam>
    public abstract class TestClassBase<T> : ITestClassBase
    {
        private bool _disposedValue;
        private readonly Lazy<IServiceProvider> _serviceProviderLazy;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestClassBase{T}"/> class.
        /// This constructor sets up the shared test context and output helper for derived test classes.
        /// </summary>
        protected TestClassBase()
        {
            TestOutputHelper = new CoreTestOutputHelper(this, Console.Out);
            _serviceProviderLazy = new Lazy<IServiceProvider>(() => BuildServiceProvider(new ServiceCollection(), TestOutputHelper));
        }

        /// <inheritdoc />
        public IServiceProvider ServiceProvider => _serviceProviderLazy.Value;

        /// <inheritdoc />
        public ICoreTestOutputHelper TestOutputHelper { get; }

        /// <inheritdoc />
        public string TestQualifiedName => typeof(T).GetLoggerCategoryName();

        /// <inheritdoc />
        public string TestDisplayName => TestQualifiedName.RemoveNamespace();

        protected virtual IServiceProvider BuildServiceProvider(IServiceCollection services, ICoreTestOutputHelper testOutputHelper)
        {
            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Releases all resources used by the <see cref="TestClassBase{T}"/> instance.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    TestOutputHelper.Dispose();
                }

                _disposedValue = true;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
