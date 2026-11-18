#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Architecture;

public class CorePackageGraphTests
{
    private static readonly string RepoRoot = FindRepoRoot(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);

    [Fact]
    public void When_a_project_references_only_core_should_have_no_new_package_dependency()
    {
        // Arrange
        var assetsPath = Path.Combine(RepoRoot, "src", "Paramore.Brighter", "obj", "project.assets.json");
        var expectedPath = Path.Combine(RepoRoot, "tests", "Paramore.Brighter.Core.Tests", "Architecture", "ExpectedCorePackages.txt");

        // Act
        var actual = PackagesByFramework(File.ReadAllText(assetsPath));
        var expected = ExpectedPackagesByFramework(File.ReadAllLines(expectedPath));

        // Assert
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var framework in expected.Keys)
            Assert.Equal(expected[framework], actual[framework]);
    }

    private static Dictionary<string, string[]> PackagesByFramework(string assetsJson)
    {
        using var document = JsonDocument.Parse(assetsJson);
        var root = document.RootElement;
        return root.GetProperty("targets").EnumerateObject()
            .ToDictionary(
                target => target.Name,
                target => target.Value.EnumerateObject()
                    .Where(entry => entry.Value.GetProperty("type").GetString() == "package")
                    .Select(entry => entry.Name.Split('/')[0])
                    .Except(BuildOnlyPackages(root, target.Name))
                    .Order(StringComparer.Ordinal)
                    .ToArray());
    }

    private static IEnumerable<string> BuildOnlyPackages(JsonElement assets, string target)
    {
        var framework = target.Split('/')[0];
        return assets.GetProperty("project").GetProperty("frameworks").GetProperty(framework).GetProperty("dependencies")
            .EnumerateObject()
            .Where(dependency => dependency.Value.TryGetProperty("suppressParent", out var suppressed) && suppressed.GetString() == "All")
            .Select(dependency => dependency.Name);
    }

    private static Dictionary<string, string[]> ExpectedPackagesByFramework(string[] lines)
    {
        var packages = new Dictionary<string, List<string>>();
        List<string>? current = null;
        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            if (line.StartsWith('['))
                packages[line.Trim('[', ']')] = current = [];
            else
                current!.Add(line.Trim());
        }

        return packages.ToDictionary(p => p.Key, p => p.Value.Order(StringComparer.Ordinal).ToArray());
    }

    private static string FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (dir.GetFiles("CLAUDE.md").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException($"Cannot find repo root from {start}");
    }
}
