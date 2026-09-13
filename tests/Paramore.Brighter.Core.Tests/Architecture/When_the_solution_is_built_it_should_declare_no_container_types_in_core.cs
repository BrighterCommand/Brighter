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
using System.Xml.Linq;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Architecture;

public class DependencyBoundaryTests
{
    private static readonly string RepoRoot = FindRepoRoot(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);

    [Fact]
    public void When_the_core_assemblys_public_surface_is_inspected_no_member_should_mention_IServiceProvider()
    {
        // Arrange — AC-22 clause 1: no public interface member in Paramore.Brighter names IServiceProvider
        var coreAssembly = typeof(IAmAScope).Assembly;

        // Act
        var violations = coreAssembly.GetExportedTypes()
            .SelectMany(t => t.GetMembers(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(MentionsServiceProvider)
            .Select(m => $"{m.DeclaringType!.FullName}.{m.Name}")
            .ToList();

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void When_the_project_files_are_parsed_no_forbidden_package_references_should_appear()
    {
        // Arrange — AC-22 clause 2: three csproj files have no forbidden references
        var coreProj = XDocument.Load(Path.Combine(RepoRoot, "src", "Paramore.Brighter", "Paramore.Brighter.csproj"));
        var diProj = XDocument.Load(Path.Combine(RepoRoot, "src", "Paramore.Brighter.Extensions.DependencyInjection",
            "Paramore.Brighter.Extensions.DependencyInjection.csproj"));
        var saProj = XDocument.Load(Path.Combine(RepoRoot, "src", "Paramore.Brighter.ServiceActivator",
            "Paramore.Brighter.ServiceActivator.csproj"));

        // Act
        var coreContainerRefs = coreProj.Descendants()
            .Where(e => e.Name.LocalName is "PackageReference" or "ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? "")
            .Where(v => v.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var diAspNetRefs = diProj.Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? "")
            .Where(v => v.StartsWith("Microsoft.AspNetCore.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var saProjectRefs = saProj.Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .ToList();

        // Assert
        Assert.Empty(coreContainerRefs);
        Assert.Empty(diAspNetRefs);
        Assert.Single(saProjectRefs);
    }

    [Fact]
    public void When_the_core_source_files_are_scanned_no_container_types_should_appear()
    {
        // Arrange — AC-22 clause 3 (load-bearing): Microsoft.Extensions.DependencyInjection is already
        // on core's compile closure transitively, so clause 2 alone does not prevent container types
        // appearing in core's source.
        var coreSourceDir = Path.Combine(RepoRoot, "src", "Paramore.Brighter");
        var forbiddenTypes = new[] { "ServiceLifetime", "IServiceCollection", "IServiceProvider", "ServiceDescriptor" };

        // Act
        var violations = Directory.EnumerateFiles(coreSourceDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, idx) => (file, line, idx))
                .Where(t => forbiddenTypes.Any(forbidden => t.line.Contains(forbidden, StringComparison.Ordinal))))
            .Select(t => $"{Path.GetRelativePath(RepoRoot, t.file)}:{t.idx + 1}: {t.line.Trim()}")
            .ToList();

        // Assert
        Assert.Empty(violations);
    }

    private static bool MentionsServiceProvider(MemberInfo member)
    {
        return member switch
        {
            MethodInfo m => ContainsServiceProvider(m.ReturnType) ||
                            m.GetParameters().Any(p => ContainsServiceProvider(p.ParameterType)),
            PropertyInfo p => ContainsServiceProvider(p.PropertyType),
            ConstructorInfo c => c.GetParameters().Any(p => ContainsServiceProvider(p.ParameterType)),
            FieldInfo f => ContainsServiceProvider(f.FieldType),
            EventInfo e => e.EventHandlerType is not null && ContainsServiceProvider(e.EventHandlerType),
            _ => false
        };
    }

    private static bool ContainsServiceProvider(Type type)
    {
        if (type.Name == "IServiceProvider")
            return true;
        if (type.IsGenericType)
            return type.GetGenericArguments().Any(ContainsServiceProvider);
        if (type.IsArray && type.GetElementType() is { } element)
            return ContainsServiceProvider(element);
        return false;
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
