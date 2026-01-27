using System;
using Paramore.Test.Helpers.TestOutput;
using Xunit.Sdk;

namespace Paramore.Test.Helpers.Base
{
    /// <summary>
    /// Defines the base interface for test classes in the AOT test suite.
    /// Provides essential properties and functionality required for test execution and setup.
    /// </summary>
    public interface ITestClassBase : IDisposable
    {
        /// <summary>
        /// Gets the test output helper used for capturing and managing test output during execution.
        /// </summary>
        /// <value>
        /// An instance of <see cref="ICoreTestOutputHelper"/> 
        /// that provides functionality for writing and managing test output.
        /// </value>
        ICoreTestOutputHelper TestOutputHelper { get; }

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> instance used for resolving dependencies
        /// within the test class. This property provides access to the service container
        /// configured for the test environment.
        /// </summary>
        IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Gets the current xUnit test instance associated with the test execution.
        /// </summary>
        /// <value>
        /// An instance of <see cref="ITest"/> representing the current test,
        /// or <c>null</c> if no test is associated.
        /// </value>
        /// <remarks>
        /// This property provides access to the xUnit test metadata, such as the test's display name
        /// and other related information. It may return <c>null</c> if the test context is not initialized.
        /// </remarks>
        ITest? XunitTest { get; }

        /// <summary>
        /// Gets the fully qualified name of the test, including its namespace and class name.
        /// This property is useful for uniquely identifying a test within the test suite.
        /// </summary>
        string TestQualifiedName { get; }

        /// <summary>
        /// Gets the display name of the test, excluding its namespace.
        /// This property provides a simplified and readable name for the test,
        /// which is particularly useful for logging and output purposes.
        /// </summary>
        string TestDisplayName { get; }
    }
}
