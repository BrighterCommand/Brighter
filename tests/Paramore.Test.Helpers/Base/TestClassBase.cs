using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Test.Helpers.Extensions;
using Paramore.Test.Helpers.TestOutput;
using Xunit;
using Xunit.Sdk;

namespace Paramore.Test.Helpers.Base
{
    /// <summary>
    /// Serves as a base class for test classes in the AOT test suite.
    /// This abstract class provides shared setup and utility functionality for derived test classes.
    /// Implements the <see cref="Xunit.IClassFixture{TFixture}"/> interface to support shared test context.
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
        /// This constructor is used to set up the shared test context and output helper for derived test classes.
        /// </summary>
        /// <param name="testOutputHelper">
        /// An instance of <see cref="ITestOutputHelper"/> used to capture test output during execution.
        /// </param>
        protected TestClassBase(ITestOutputHelper testOutputHelper)
        {
            ArgumentNullException.ThrowIfNull(testOutputHelper);

            TestOutputHelper = new CoreTestOutputHelper(this, testOutputHelper);
            _serviceProviderLazy = new Lazy<IServiceProvider>(() => BuildServiceProvider(new ServiceCollection(), TestOutputHelper));
        }

        /// <inheritdoc />
        public IServiceProvider ServiceProvider => _serviceProviderLazy.Value;

        /// <inheritdoc />
        public ICoreTestOutputHelper TestOutputHelper { get; }

        /// <inheritdoc />
        public ITest? XunitTest => (ITest?)GetTestField(TestOutputHelper.WrappedTestOutputHelper)?.GetValue(TestOutputHelper.WrappedTestOutputHelper);

        /// <inheritdoc />
        public string TestQualifiedName => XunitTest?.TestDisplayName ?? typeof(T).GetLoggerCategoryName();

        /// <inheritdoc />
        public string TestDisplayName => TestQualifiedName.RemoveNamespace();

        protected virtual IServiceProvider BuildServiceProvider(IServiceCollection services, ICoreTestOutputHelper testOutputHelper)
        {
            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Releases all resources used by the <see cref="TestClassBase{T}"/> instance.
        /// </summary>
        /// <remarks>
        /// This method is responsible for performing cleanup operations for the current instance of the class.
        /// It ensures that any unmanaged resources are released and any disposable objects are properly disposed.
        /// </remarks>
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

        /// <summary>
        /// Retrieves the private field named 'test' from the specified <see cref="ITestOutputHelper"/> instance.
        /// </summary>
        /// <param name="testOutputHelper">
        /// An instance of <see cref="ITestOutputHelper"/> from which the private field is to be retrieved.
        /// </param>
        /// <returns>
        /// A <see cref="FieldInfo"/> object representing the 'test' field if it exists; otherwise, <c>null</c>.
        /// </returns>
        /// <remarks>
        /// This method uses reflection to access a private field named 'test' within the provided 
        /// <see cref="ITestOutputHelper"/> instance. The field is expected to exist in the implementation.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the <paramref name="testOutputHelper"/> parameter is <c>null</c>.
        /// </exception>
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075", Justification = "The field 'test' is known to exist in the ITestOutputHelper implementation.")]
        private static FieldInfo? GetTestField(ITestOutputHelper testOutputHelper)
        {
            return testOutputHelper.GetType().GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
