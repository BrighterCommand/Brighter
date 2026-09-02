using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// A single Skip-convention violation found during the tree scan.
/// </summary>
/// <param name="FilePath">Absolute path of the offending file.</param>
/// <param name="LineNumber">1-based line number of the violating Skip attribute.</param>
/// <param name="SkipValue">The literal value that failed the Deferred: #&lt;n&gt; check.</param>
public sealed record SkipViolation(string FilePath, int LineNumber, string SkipValue);

/// <summary>
/// The result of a tree scan.
/// </summary>
/// <param name="FilesScanned">Total files visited (templates + generated copies).</param>
/// <param name="ConformingSkipsFound">Count of Skip values that matched Deferred: #&lt;n&gt;.</param>
/// <param name="Violations">Every Skip value that failed the pattern check.</param>
public sealed record ScanResult(
    int FilesScanned,
    int ConformingSkipsFound,
    IReadOnlyList<SkipViolation> Violations);

/// <summary>
/// Read-only, network-free audit of the greppable linked-issue Skip convention (ADR 0067).
///
/// Rule: any Skip attribute present in a messaging-gateway in-tree artifact must start with
/// "Deferred: #" followed by one or more decimal digits — e.g. "Deferred: #4240 — …".
/// A bare reason ("flaky"), an empty value, or the NNNN placeholder used before issue
/// reconciliation are all violations.
///
/// Scanned paths (in-tree only — no network, no subprocess):
///   tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/**/*.cs.liquid
///   tests/Paramore.Brighter.*.Tests/**/Generated/**/*.cs
/// </summary>
public static class GatewaySkipConventionAudit
{
    // Must start with "Deferred: #" followed by at least one decimal digit.
    private static readonly Regex DEFERRED_PATTERN =
        new(@"^Deferred: #\d+", RegexOptions.Compiled);

    // Captures the string literal after Skip = "…" on a single line.
    // Whitespace around '=' is optional (Skip="flaky" occurs in hand-written tests) and the
    // capture is [^"]* so that a reasonless Skip = "" is surfaced as a violation rather than
    // slipping past the extractor — AC-13 forbids a silent skip, and the empty value is the
    // most silent of all.
    private static readonly Regex SKIP_EXTRACTOR =
        new(@"Skip\s*=\s*""([^""]*)""", RegexOptions.Compiled);

    // Liquid template placeholder — this is template syntax, not a real Skip value.
    private const string LIQUID_SKIP_PLACEHOLDER = "{{ Skip }}";

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> satisfies the Deferred: #&lt;n&gt; pattern.
    /// </summary>
    public static bool IsConformingSkipValue(string value)
        => !string.IsNullOrEmpty(value) && DEFERRED_PATTERN.IsMatch(value);

    /// <summary>
    /// Scans all in-tree messaging-gateway artifacts under <paramref name="repoRoot"/> and returns
    /// the aggregate result. Never makes network calls or spawns subprocesses.
    /// </summary>
    public static ScanResult ScanTree(string repoRoot)
    {
        var violations = new List<SkipViolation>();
        var filesScanned = 0;
        var conformingSkipsFound = 0;

        foreach (var file in EnumerateGatewayArtifacts(repoRoot))
        {
            filesScanned++;
            foreach (var (lineNumber, value) in ExtractSkipValues(file))
            {
                if (IsConformingSkipValue(value))
                    conformingSkipsFound++;
                else
                    violations.Add(new SkipViolation(file, lineNumber, value));
            }
        }

        return new ScanResult(filesScanned, conformingSkipsFound, violations);
    }

    /// <summary>
    /// Enumerates every in-tree messaging-gateway artifact the conformance audits scan: the
    /// gateway templates and every generated copy. This is the single definition of that scope —
    /// <see cref="LedgerSkipCrossCheckAudit"/> walks the same set through this method, so a change
    /// to the artifact layout cannot silently desync the two audits.
    /// </summary>
    public static IEnumerable<string> EnumerateGatewayArtifacts(string repoRoot)
    {
        // ── Templates ─────────────────────────────────────────────────────────
        var templateRoot = Path.Combine(
            repoRoot, "tools", "Paramore.Brighter.Test.Generator",
            "Templates", "MessagingGateway");

        if (Directory.Exists(templateRoot))
        {
            foreach (var file in Directory.EnumerateFiles(
                         templateRoot, "*.cs.liquid", SearchOption.AllDirectories))
                yield return file;
        }

        // ── Generated copies ──────────────────────────────────────────────────
        var testsRoot = Path.Combine(repoRoot, "tests");
        if (!Directory.Exists(testsRoot))
            yield break;

        foreach (var testProject in Directory.EnumerateDirectories(
                     testsRoot, "Paramore.Brighter.*.Tests"))
        {
            foreach (var generatedDir in Directory.EnumerateDirectories(
                         testProject, "Generated", SearchOption.AllDirectories))
            {
                foreach (var file in Directory.EnumerateFiles(
                             generatedDir, "*.cs", SearchOption.AllDirectories))
                    yield return file;
            }
        }
    }

    /// <summary>
    /// Yields every real Skip value in <paramref name="filePath"/> as a 1-based line number and
    /// its literal value. The Liquid <c>{{ Skip }}</c> placeholder is template syntax rendered at
    /// generation time, not a value, so it is not yielded.
    /// </summary>
    public static IEnumerable<(int LineNumber, string Value)> ExtractSkipValues(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        for (var i = 0; i < lines.Length; i++)
        {
            var match = SKIP_EXTRACTOR.Match(lines[i]);
            if (!match.Success)
                continue;

            var value = match.Groups[1].Value;
            if (value == LIQUID_SKIP_PLACEHOLDER)
                continue;

            yield return (i + 1, value);
        }
    }
}
