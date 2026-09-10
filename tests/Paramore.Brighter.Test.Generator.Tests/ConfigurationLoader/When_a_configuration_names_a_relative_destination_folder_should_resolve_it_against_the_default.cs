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

using System;
using System.IO;
using Xunit;
using Loader = Paramore.Brighter.Test.Generator.Configuration.TestConfigurationLoader;

namespace Paramore.Brighter.Test.Generator.Tests.ConfigurationLoader;

/// <summary>
/// The loader exists so that the generator and the generated-tree audit read a configuration by
/// the same rules. They run from different directories - <c>generate-test.sh</c> puts the
/// generator in the project folder, while the audit runs wherever the test host does - so a
/// destination folder left relative would mean the same configuration naming two different
/// folders depending on who read it, and the audit would report a project's whole tree as
/// orphaned and the same files as missing. The base is therefore the configuration file's own
/// folder, which is the one thing both readers agree on without having to run from the same
/// directory.
/// </summary>
public class DestinationFolderResolutionTests : IDisposable
{
    private readonly string _projectFolder =
        Path.Combine(Path.GetTempPath(), $"LoaderTests_{Guid.NewGuid()}");

    public DestinationFolderResolutionTests() => Directory.CreateDirectory(_projectFolder);

    [Fact]
    public void When_a_configuration_names_a_relative_destination_folder_should_resolve_it_against_the_default()
    {
        // Arrange
        var configurationFile = WriteConfiguration("""
            {
              "Namespace": "Sample.Tests",
              "DestinationFolder": "../Sibling.Tests"
            }
            """);

        // Act
        var configuration = Loader.Load(configurationFile, defaultDestinationFolder: _projectFolder);

        // Assert - resolved, not left relative for the caller's current directory to decide
        Assert.NotNull(configuration);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(_projectFolder, "..", "Sibling.Tests")),
            configuration.DestinationFolder);
    }

    [Fact]
    public void When_the_default_folder_is_not_the_configurations_own_folder_should_resolve_against_the_configuration()
    {
        // Arrange - the two bases pulled apart, which is what running the generator from the
        // repository root against tests/Foo/test-configuration.json does
        var configurationFile = WriteConfiguration("""
            {
              "Namespace": "Sample.Tests",
              "DestinationFolder": "../Sibling.Tests"
            }
            """);
        // Nested inside the project folder rather than beside it: two sibling temp folders both
        // resolve "../Sibling.Tests" to the same place, so a test using one would pass either way
        var elsewhere = Path.Combine(_projectFolder, "nested", "deeper");

        // Act
        var configuration = Loader.Load(configurationFile, defaultDestinationFolder: elsewhere);

        // Assert - the configuration's own folder wins, so two callers running from different
        // directories read the same configuration the same way
        Assert.NotNull(configuration);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(_projectFolder, "..", "Sibling.Tests")),
            configuration.DestinationFolder);
    }

    [Fact]
    public void When_the_default_folder_is_relative_should_still_return_an_absolute_folder()
    {
        // Arrange - the loader documents that what it hands back is always absolute
        var configurationFile = WriteConfiguration("""
            {
              "Namespace": "Sample.Tests"
            }
            """);

        // Act
        var configuration = Loader.Load(configurationFile, defaultDestinationFolder: "relative/output");

        // Assert
        Assert.NotNull(configuration);
        Assert.True(Path.IsPathRooted(configuration.DestinationFolder));
        Assert.Equal(Path.GetFullPath("relative/output"), configuration.DestinationFolder);
    }

    [Fact]
    public void When_a_configuration_names_no_destination_folder_should_use_the_default()
    {
        // Arrange
        var configurationFile = WriteConfiguration("""
            {
              "Namespace": "Sample.Tests"
            }
            """);

        // Act
        var configuration = Loader.Load(configurationFile, defaultDestinationFolder: _projectFolder);

        // Assert
        Assert.NotNull(configuration);
        Assert.Equal(_projectFolder, configuration.DestinationFolder);
    }

    [Fact]
    public void When_a_configuration_names_an_absolute_destination_folder_should_keep_it()
    {
        // Arrange
        var elsewhere = Path.Combine(Path.GetTempPath(), $"LoaderTestsElsewhere_{Guid.NewGuid()}");
        var configurationFile = WriteConfiguration($$"""
            {
              "Namespace": "Sample.Tests",
              "DestinationFolder": {{System.Text.Json.JsonSerializer.Serialize(elsewhere)}}
            }
            """);

        // Act
        var configuration = Loader.Load(configurationFile, defaultDestinationFolder: _projectFolder);

        // Assert - an absolute path has nothing to resolve against, so it is carried through
        Assert.NotNull(configuration);
        Assert.Equal(Path.GetFullPath(elsewhere), configuration.DestinationFolder);
    }

    private string WriteConfiguration(string json)
    {
        var configurationFile = Path.Combine(_projectFolder, Loader.ConfigurationFileName);
        File.WriteAllText(configurationFile, json);
        return configurationFile;
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectFolder))
        {
            Directory.Delete(_projectFolder, recursive: true);
        }
    }
}
