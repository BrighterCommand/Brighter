using System;
using Paramore.Test.Helpers.Base;
using Xunit;

namespace Paramore.Test.Helpers.TestOutput
{
    public interface ICoreTestOutputHelper : ITestOutputHelper, IDisposable
    {
        /// <summary>
        /// Gets the test case associated with the current test output helper.
        /// </summary>
        /// <remarks>
        /// This property provides access to the <see cref="ITestClassBase"/> 
        /// instance representing the test case. It is used to retrieve information and dependencies 
        /// related to the test being executed.
        /// </remarks>
        ITestClassBase TestCase { get; }

        /// <summary>
        /// Gets the wrapped instance of <see cref="ITestOutputHelper"/> used for test output handling.
        /// </summary>
        ITestOutputHelper WrappedTestOutputHelper { get; }

        /// <summary>
        /// Gets the UTC date and time when the test output helper was initialized.
        /// </summary>
        /// <value>
        /// A <see cref="DateTime"/> representing the start time of the test output helper.
        /// </value>
        DateTime DateTimeStart { get; }

        /// <summary>
        /// Gets the accumulated output written by the test output helper.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> containing the concatenated output messages.
        /// </value>
        string Output { get; }

        /// <summary>
        /// Writes a message to the test output.
        /// </summary>
        /// <param name="message">The message to write.</param>
        void Write(string message);

        /// <summary>
        /// Writes a formatted message to the test output.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An array of objects to format.</param>
        /// <remarks>
        /// This method formats the specified string and arguments, then writes the resulting message to the test output.
        /// </remarks>
        void Write(string format, params object[] args);

        /// <summary>
        /// Writes an empty line to the test output.
        /// </summary>
        void WriteLine();

        /// <summary>
        /// Writes a log message to the test output. If it is the first log message, it also writes an opening bracket.
        /// </summary>
        /// <param name="message">The log message to write.</param>
        /// <remarks>
        /// This method ensures thread safety when writing log messages and handles exceptions gracefully.
        /// </remarks>
        void WriteLogLine(string message);

        /// <summary>
        /// Writes a boolean value followed by a line terminator to the test output.
        /// </summary>
        /// <param name="value">The boolean value to write.</param>
        void WriteLine(bool value);

        /// <summary>
        /// Writes a character followed by a line terminator to the test output.
        /// </summary>
        /// <param name="value">The character to write.</param>
        void WriteLine(char value);

        /// <summary>
        /// Writes a decimal value followed by a line terminator to the test output.
        /// </summary>
        /// <param name="value">The decimal value to write.</param>
        /// <remarks>
        /// This method ensures thread safety when writing the value to the test output.
        /// </remarks>
        void WriteLine(decimal value);

        /// <summary>
        /// Writes a double-precision floating-point number followed by a line terminator to the test output.
        /// </summary>
        /// <param name="value">The double-precision floating-point number to write.</param>
        /// <remarks>
        /// This method inherits its behavior from the base implementation and ensures thread safety when writing the value.
        /// </remarks>
        void WriteLine(double value);

        /// <summary>
        /// Writes a single-precision floating-point number followed by a line terminator to the test output.
        /// </summary>
        /// <param name="value">The single-precision floating-point number to write.</param>
        /// <remarks>
        /// This method inherits its behavior from the base implementation and ensures thread safety when writing the value.
        /// </remarks>
        void WriteLine(float value);

        /// <summary>
        /// Writes a 32-bit signed integer followed by a line terminator to the test output.
        /// </summary>
        /// <param name="value">The 32-bit signed integer to write.</param>
        /// <remarks>
        /// This method ensures thread safety when writing the value to the test output.
        /// </remarks>
        void WriteLine(int value);

        /// <summary>
        /// Writes an unsigned integer value followed by a line terminator to the test output.
        /// </summary>
        /// <param name="value">The unsigned integer value to write.</param>
        /// <remarks>
        /// This method utilizes the string representation of the unsigned integer value
        /// and ensures thread safety when writing to the test output.
        /// </remarks>
        void WriteLine(uint value);

        /// <summary>
        /// Writes a 64-bit signed integer followed by a line terminator to the test output.
        /// </summary>
        /// <param name="value">The 64-bit signed integer to write.</param>
        /// <remarks>
        /// This method delegates the writing operation to the string-based method.
        /// </remarks>
        void WriteLine(long value);

        /// <summary>
        /// Writes an unsigned long integer value followed by a line terminator to the test output.
        /// </summary>
        /// <param name="value">The unsigned long integer value to write.</param>
        /// <remarks>
        /// This method delegates the conversion of the value to its string representation
        /// and writes it to the test output.
        /// </remarks>
        void WriteLine(ulong value);

        /// <summary>
        /// Writes the string representation of the specified object, followed by a line terminator, to the test output.
        /// </summary>
        /// <param name="value">The object whose string representation is to be written. If <c>null</c>, an empty string is written.</param>
        /// <remarks>
        /// This method ensures that <c>null</c> values are handled gracefully by converting them to an empty string before writing.
        /// </remarks>
        void WriteLine(object? value);
    }
}
