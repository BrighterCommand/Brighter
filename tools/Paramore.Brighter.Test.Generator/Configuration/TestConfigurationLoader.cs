#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.IO;
using System.Text.Json;

namespace Paramore.Brighter.Test.Generator.Configuration;

/// <summary>
/// Reads a <c>test-configuration.json</c> file into the <see cref="TestConfiguration"/> the
/// generators render from.
/// </summary>
/// <remarks>
/// Turning a file into a configuration is stated once, here, because more than one caller does it:
/// the generator when it writes the tree, and the audit when it asks what the tree should hold. Two
/// statements of it would agree until one of them was changed - which had already happened, over
/// whether an explicit <see cref="TestConfiguration.DestinationFolder"/> is honoured.
/// </remarks>
/// <remarks>
/// The <see cref="TestConfiguration.DestinationFolder"/> a caller receives is always absolute, so
/// that the paths built from it are too, whichever directory the caller happens to run in.
/// </remarks>
public static class TestConfigurationLoader
{
    /// <summary>
    /// The name a project's configuration file is expected to have.
    /// </summary>
    public const string ConfigurationFileName = "test-configuration.json";

    // Pinned rather than left to the ambient default, so that both callers deserialize by the same
    // rules. These are the defaults the generator has always read configurations with.
    private static readonly JsonSerializerOptions s_serializerOptions = new(JsonSerializerDefaults.General);

    /// <summary>
    /// Reads the configuration at <paramref name="configurationFilePath"/>.
    /// </summary>
    /// <param name="configurationFilePath">The path of the JSON configuration file to read.</param>
    /// <param name="defaultDestinationFolder">
    /// The folder generated files are written to when the configuration does not name one, and the
    /// folder a relative <see cref="TestConfiguration.DestinationFolder"/> is resolved against. Must
    /// be an absolute path.
    /// </param>
    /// <returns>
    /// The configuration, or <c>null</c> if the file holds the JSON literal <c>null</c>.
    /// </returns>
    /// <exception cref="JsonException">The file is not valid JSON for a configuration.</exception>
    public static TestConfiguration? Load(string configurationFilePath, string defaultDestinationFolder)
    {
        using var stream = File.OpenRead(configurationFilePath);
        var configuration = JsonSerializer.Deserialize<TestConfiguration>(stream, s_serializerOptions);
        if (configuration == null)
        {
            return null;
        }

        // Resolved here rather than left to each caller, because a relative value would otherwise
        // resolve against whatever the current directory happened to be: the project folder for the
        // generator, which generate-test.sh cd's into, and the test host's folder for the audit.
        // Those are different folders, and the audit would then report a project's whole tree as
        // orphaned and an equal number of files as missing.
        configuration.DestinationFolder = string.IsNullOrEmpty(configuration.DestinationFolder)
            ? defaultDestinationFolder
            : Path.GetFullPath(configuration.DestinationFolder, defaultDestinationFolder);

        return configuration;
    }
}
