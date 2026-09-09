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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Tests.GeneratedFileAudit;

/// <summary>
/// Compares the files the test generator would write for the checked-in configurations with the
/// files that are actually on disk under a <c>Generated/</c> directory, in both directions.
/// </summary>
/// <remarks>
/// <para>
/// The two directions are different questions and neither subsumes the other. An
/// <see cref="Orphans">orphan</see> is a file the tree holds that no configuration asks for -
/// what a flipped capability flag, a deleted template or a dropped generation code path leaves
/// behind, because the generator writes files and never removes one. A
/// <see cref="Missing">missing</see> file is one a configuration asks for that the tree does not
/// hold - what a project that was never regenerated leaves behind.
/// </para>
/// <para>
/// The expected set comes from the generators themselves, through
/// <see cref="Generators.OutboxGenerator.Plan"/> and
/// <see cref="Generators.MessagingGatewayGenerator.Plan"/>, rather than from a description of them
/// kept here. A second description would be a second thing to keep in step, and would agree with
/// the generator exactly until the day it mattered.
/// </para>
/// <para>
/// Scope is the <c>Generated/</c> tree, which is generator-owned in full. The shared files a
/// generator writes to a project root sit alongside hand-written code and are not audited.
/// </para>
/// </remarks>
public sealed class GeneratedTreeAudit
{
    private const string GENERATED_FOLDER_NAME = "Generated";
    private const string SOLUTION_FILE_NAME = "Brighter.slnx";

    // Directories whose contents are build output rather than source, and so are never audited.
    private static readonly string[] BUILD_OUTPUT_FOLDER_NAMES = ["bin", "obj"];

    private GeneratedTreeAudit(IReadOnlySet<string> expected, IReadOnlySet<string> onDisk)
    {
        Expected = expected;
        OnDisk = onDisk;

        Orphans = Sorted(onDisk.Except(expected, StringComparer.Ordinal));
        Missing = Sorted(expected.Except(onDisk, StringComparer.Ordinal));
    }

    /// <summary>
    /// Audits the test projects that are direct children of <paramref name="testsRoot"/>.
    /// </summary>
    /// <remarks>
    /// The reading of configurations and the walk of the tree happen here rather than in a
    /// constructor, so that a failure in either is attributable to an operation with a name.
    /// </remarks>
    /// <param name="testsRoot">
    /// The directory holding the test projects - the repository's <c>tests</c> folder, which is
    /// what <c>generate-test.sh</c> walks.
    /// </param>
    /// <returns>The audit of that tree.</returns>
    public static GeneratedTreeAudit Of(string testsRoot) =>
        new(ExpectedFilesUnder(testsRoot), GeneratedFilesUnder(testsRoot));

    /// <summary>
    /// The files the checked-in configurations would have the generators write into a
    /// <c>Generated/</c> directory.
    /// </summary>
    public IReadOnlySet<string> Expected { get; }

    /// <summary>
    /// The files that are on disk under a <c>Generated/</c> directory.
    /// </summary>
    public IReadOnlySet<string> OnDisk { get; }

    /// <summary>
    /// Files on disk that no configuration asks for. The generator cannot remove these; a person
    /// has to, once they have established which of the tree and the configuration is the wrong one.
    /// </summary>
    public IReadOnlyList<string> Orphans { get; }

    /// <summary>
    /// Files a configuration asks for that are absent from the tree. Regenerating produces them.
    /// </summary>
    public IReadOnlyList<string> Missing { get; }

    /// <summary>
    /// Finds the repository's <c>tests</c> folder by walking up from the running assembly.
    /// </summary>
    /// <remarks>
    /// The marker is the solution file, which is what makes a directory the repository root. A
    /// particular test project would be a marker that someone could retire or rename without ever
    /// meaning to move the root, and the audit would then throw rather than audit.
    /// </remarks>
    /// <returns>The absolute path of the <c>tests</c> folder.</returns>
    /// <exception cref="InvalidOperationException">The folder could not be found.</exception>
    public static string LocateTestsRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory != null;
             directory = directory.Parent)
        {
            var testsRoot = Path.Combine(directory.FullName, "tests");
            if (File.Exists(Path.Combine(directory.FullName, SOLUTION_FILE_NAME))
                && Directory.Exists(testsRoot))
            {
                return testsRoot;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate the repository's tests folder by walking up from " +
            $"{AppContext.BaseDirectory}; expected to find a directory holding both " +
            $"{SOLUTION_FILE_NAME} and a tests folder.");
    }

    /// <summary>
    /// Asks each generator what it would write for every configuration under
    /// <paramref name="testsRoot"/>, keeping only the files that land in a <c>Generated/</c>
    /// directory.
    /// </summary>
    private static IReadOnlySet<string> ExpectedFilesUnder(string testsRoot)
    {
        var outboxGenerator = new Generators.OutboxGenerator(
            NullLogger<Generators.OutboxGenerator>.Instance);
        var messagingGatewayGenerator = new Generators.MessagingGatewayGenerator(
            NullLogger<Generators.MessagingGatewayGenerator>.Instance);

        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var projectFolder in Directory.EnumerateDirectories(testsRoot))
        {
            var configurationFile = Path.Combine(
                projectFolder, TestConfigurationLoader.ConfigurationFileName);
            if (!File.Exists(configurationFile))
            {
                continue;
            }

            // generate-test.sh runs the generator from the project folder, so the folder the
            // generator would default its destination to is that project's folder. Loaded through
            // the generator's own loader, so that the audit cannot read a configuration by
            // different rules from the run it is auditing.
            var configuration = TestConfigurationLoader.Load(
                configurationFile, defaultDestinationFolder: projectFolder);
            if (configuration == null)
            {
                continue;
            }

            expected.UnionWith(outboxGenerator.Plan(configuration)
                .Concat(messagingGatewayGenerator.Plan(configuration))
                .Select(plannedFile => Path.GetFullPath(plannedFile.DestinationPath))
                .Where(IsUnderAGeneratedFolder));
        }

        return expected;
    }

    /// <summary>
    /// Every C# file on disk under a <c>Generated/</c> directory, anywhere beneath
    /// <paramref name="testsRoot"/>.
    /// </summary>
    /// <remarks>
    /// Scanning the whole tree rather than only the projects that carry a configuration is
    /// deliberate: a project whose configuration was deleted is exactly the case where the output
    /// left behind should be reported, not overlooked.
    /// </remarks>
    private static IReadOnlySet<string> GeneratedFilesUnder(string testsRoot)
    {
        var onDisk = new HashSet<string>(StringComparer.Ordinal);
        Collect(new DirectoryInfo(testsRoot), underGeneratedFolder: false, onDisk);
        return onDisk;
    }

    /// <summary>
    /// Adds every C# file at or below <paramref name="directory"/> that sits under a
    /// <c>Generated/</c> directory, descending past build output rather than into it.
    /// </summary>
    /// <remarks>
    /// Pruning on the way down rather than filtering the results is what keeps the walk cheap: a
    /// Release build leaves a copy of much of the tree under <c>bin</c> and <c>obj</c>, and the
    /// audit runs after one in CI.
    /// </remarks>
    private static void Collect(DirectoryInfo directory, bool underGeneratedFolder, ISet<string> onDisk)
    {
        foreach (var child in directory.EnumerateDirectories())
        {
            if (BUILD_OUTPUT_FOLDER_NAMES.Contains(child.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            Collect(child,
                underGeneratedFolder
                || string.Equals(child.Name, GENERATED_FOLDER_NAME, StringComparison.Ordinal),
                onDisk);
        }

        if (!underGeneratedFolder)
        {
            return;
        }

        foreach (var file in directory.EnumerateFiles("*.cs"))
        {
            onDisk.Add(Path.GetFullPath(file.FullName));
        }
    }

    private static bool IsUnderAGeneratedFolder(string path) =>
        Segments(path).SkipLast(1).Contains(GENERATED_FOLDER_NAME, StringComparer.Ordinal);

    private static string[] Segments(string path) =>
        path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

    private static IReadOnlyList<string> Sorted(IEnumerable<string> paths) =>
        paths.OrderBy(path => path, StringComparer.Ordinal).ToList();
}
