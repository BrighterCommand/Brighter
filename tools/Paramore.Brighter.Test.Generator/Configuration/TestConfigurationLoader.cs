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
/// The one reader of a configuration file, shared by the generator and the generated-tree audit.
/// The <see cref="TestConfiguration.DestinationFolder"/> it returns is always absolute.
/// </remarks>
public static class TestConfigurationLoader
{
    /// <summary>
    /// The name a project's configuration file is expected to have.
    /// </summary>
    public const string ConfigurationFileName = "test-configuration.json";

    // Pinned so that both callers deserialize by the same rules.
    private static readonly JsonSerializerOptions s_serializerOptions = new(JsonSerializerDefaults.General);

    /// <summary>
    /// Reads the configuration at <paramref name="configurationFilePath"/>.
    /// </summary>
    /// <param name="configurationFilePath">The path of the JSON configuration file to read.</param>
    /// <param name="defaultDestinationFolder">
    /// The folder generated files are written to when the configuration does not name one. A
    /// relative <see cref="TestConfiguration.DestinationFolder"/> is resolved against the
    /// configuration file's own folder instead, so that two callers running from different
    /// directories read the same configuration the same way.
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

        configuration.DestinationFolder = ResolveDestinationFolder(
            configuration.DestinationFolder, configurationFilePath, defaultDestinationFolder);

        configuration.ApplySharedDefaults();

        return configuration;
    }

    // A relative folder resolves against the configuration file's own folder, not the current
    // directory or the caller's default, so readers running from different folders agree.
    private static string ResolveDestinationFolder(
        string declaredFolder, string configurationFilePath, string defaultDestinationFolder)
    {
        var configurationFolder =
            Path.GetDirectoryName(Path.GetFullPath(configurationFilePath)) ?? defaultDestinationFolder;

        return string.IsNullOrEmpty(declaredFolder)
            ? Path.GetFullPath(defaultDestinationFolder)
            : Path.GetFullPath(declaredFolder, configurationFolder);
    }
}
