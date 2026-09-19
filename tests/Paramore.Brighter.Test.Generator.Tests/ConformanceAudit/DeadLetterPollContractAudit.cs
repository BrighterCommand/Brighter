#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// A single violation of the rejection-destination poll contract.
/// </summary>
/// <param name="Kind">
/// <c>"InternalRetryLoop"</c> — the helper wraps its receive in a loop of its own; or
/// <c>"InternalPollBackoff"</c> — the helper sleeps between attempts. Either makes the helper a
/// retry loop rather than the single bounded receive its callers are written against.
/// </param>
/// <param name="Provider">Provider file the helper was found in, repository-relative.</param>
/// <param name="Helper">The helper method, e.g. <c>GetMessageFromDeadLetterQueueAsync</c>.</param>
/// <param name="Detail">Human-readable description of the specific violation.</param>
public sealed record HelperPollViolation(string Kind, string Provider, string Helper, string Detail);

/// <summary>
/// The aggregate result of a rejection-destination poll-contract audit.
/// </summary>
/// <param name="HelpersScanned">Helper bodies found and read across every provider.</param>
/// <param name="Violations">Every contract violation found.</param>
public sealed record PollContractResult(
    int HelpersScanned,
    IReadOnlyList<HelperPollViolation> Violations);

/// <summary>
/// Read-only, network-free audit of the rejection-destination poll contract.
///
/// <para>Every assertion that a message *arrives* sits inside one bounded retry loop, with a
/// stated poll interval and a stated ceiling. The assertions that a message is *absent* are
/// exempt — those are a single bounded receive. Both rules are written in the generated test, and
/// both assume the same thing of the provider: that
/// <c>GetMessageFromDeadLetterQueue</c> / <c>GetMessageFromInvalidChannel</c> attempt **one**
/// bounded receive and return, leaving the retrying to the caller.</para>
///
/// <para>A helper that retries internally breaks both rules at once, and silently. It overruns the
/// caller's ceiling, so the caller's loop re-tests an already-expired stopwatch and never runs a
/// second iteration — the bounded loop becomes decorative, and the real bound is
/// whatever the helper's own loop happens to be. Worse, it turns the absence checks into the
/// most expensive tests in the suite: the message is asserted never to arrive, so an internal retry
/// loop is guaranteed to burn its entire ceiling on every single run.</para>
///
/// <para>So the contract this audit enforces is narrow and mechanical: inside a helper body there is
/// no loop and no sleep. The ceiling and the poll interval live in the generated test, where
/// they belong and where they can be read.</para>
///
/// <para>Reads provider source as text rather than reflecting over it: the audit lives in the
/// generator's test project, which does not reference the transport test projects, and a text scan
/// keeps it that way.</para>
/// </summary>
public static class DeadLetterPollContractAudit
{
    /// <summary>The helper methods the contract governs.</summary>
    /// <remarks>
    /// Both are rejection *destinations* — the places a message goes when the pump gives up on it.
    /// The channel receives a test does itself are not in scope: those already sit inside the
    /// caller's bounded loop with nothing in between.
    /// </remarks>
    public static readonly IReadOnlyList<string> GOVERNED_HELPERS =
        new[] { "GetMessageFromDeadLetterQueue", "GetMessageFromInvalidChannel" };

    // A helper signature, sync or async. The body is located by brace-matching from here rather
    // than by regex, because a regex cannot balance braces and these bodies nest.
    private static readonly Regex HELPER_SIGNATURE = new(
        @"public\s+(?:async\s+)?(?:Task<Message>|Message)\s+"
        + @"(?<name>GetMessageFrom(?:DeadLetterQueue|InvalidChannel))(?<async>Async)?\s*\(",
        RegexOptions.Compiled);

    // `do` is matched without a trailing delimiter: `do {` and `do` on its own line both count.
    private static readonly Regex RETRY_LOOP = new(
        @"\b(?<keyword>for|foreach|while)\b\s*\(|\bdo\b\s*\{",
        RegexOptions.Compiled);

    private static readonly Regex POLL_BACKOFF = new(
        @"\b(?<call>Thread\.Sleep|Task\.Delay)\s*\(",
        RegexOptions.Compiled);

    /// <summary>
    /// Audits every gateway provider under <paramref name="repoRoot"/>'s <c>tests/</c> tree.
    /// </summary>
    /// <param name="repoRoot">Repository root holding the <c>tests/</c> tree.</param>
    /// <returns>The helper bodies scanned, and every violation found.</returns>
    public static PollContractResult Audit(string repoRoot)
    {
        var violations = new List<HelperPollViolation>();
        var scanned = 0;

        foreach (var path in EnumerateProviderFiles(repoRoot))
        {
            var source = StripCommentsAndStrings(File.ReadAllText(path));
            var provider = Relative(repoRoot, path);

            foreach (var (helper, body) in EnumerateHelperBodies(source))
            {
                scanned++;
                violations.AddRange(Inspect(provider, helper, body));
            }
        }

        return new PollContractResult(scanned, violations);
    }

    /// <summary>
    /// Reports the contract violations in a single helper body.
    /// </summary>
    private static IEnumerable<HelperPollViolation> Inspect(string provider, string helper, string body)
    {
        var loop = RETRY_LOOP.Match(body);
        if (loop.Success)
        {
            var keyword = loop.Groups["keyword"].Success ? loop.Groups["keyword"].Value : "do";
            yield return new HelperPollViolation(
                "InternalRetryLoop", provider, helper,
                $"the body opens a '{keyword}' loop. The helper must attempt one bounded receive "
                + "and return; the retry belongs to the caller's NFR-2 loop, which is the only "
                + "place the ceiling and the poll interval can be read.");
        }

        var backoff = POLL_BACKOFF.Match(body);
        if (backoff.Success)
        {
            yield return new HelperPollViolation(
                "InternalPollBackoff", provider, helper,
                $"the body calls {backoff.Groups["call"].Value}. A helper that sleeps is polling, "
                + "and it overruns the caller's ceiling while doing so.");
        }
    }

    /// <summary>
    /// Every hand-written gateway provider in the tree. Generated output and build artefacts are
    /// skipped: the contract binds the providers a maintainer edits.
    /// </summary>
    private static IEnumerable<string> EnumerateProviderFiles(string repoRoot)
    {
        var testsRoot = Path.Combine(repoRoot, "tests");
        if (!Directory.Exists(testsRoot))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(testsRoot, "*Provider.cs", SearchOption.AllDirectories))
        {
            if (IsExcluded(path))
            {
                continue;
            }

            yield return path;
        }
    }

    private static bool IsExcluded(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p =>
            string.Equals(p, "obj", StringComparison.Ordinal)
            || string.Equals(p, "bin", StringComparison.Ordinal)
            || string.Equals(p, "Generated", StringComparison.Ordinal));
    }

    /// <summary>
    /// Yields each governed helper's name and body, the body located by brace-matching from the
    /// opening brace that follows the signature.
    /// </summary>
    private static IEnumerable<(string Helper, string Body)> EnumerateHelperBodies(string source)
    {
        foreach (Match signature in HELPER_SIGNATURE.Matches(source))
        {
            var name = signature.Groups["name"].Value + signature.Groups["async"].Value;

            var open = source.IndexOf('{', signature.Index + signature.Length);
            if (open < 0)
            {
                continue;
            }

            var close = MatchingBrace(source, open);
            if (close < 0)
            {
                continue;
            }

            yield return (name, source.Substring(open, close - open + 1));
        }
    }

    /// <summary>Index of the brace closing the one at <paramref name="open"/>, or -1.</summary>
    private static int MatchingBrace(string source, int open)
    {
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Blanks comments and string literals, preserving length and line structure.
    /// </summary>
    /// <remarks>
    /// Brace-matching and keyword searches both have to ignore text that only looks like code. An
    /// interpolated message such as <c>$"...{count}..."</c> would otherwise unbalance the matcher,
    /// and the word "while" in a doc comment would otherwise read as a loop. Characters are
    /// replaced with spaces rather than removed so reported offsets stay meaningful.
    /// </remarks>
    private static string StripCommentsAndStrings(string source)
    {
        var result = new StringBuilder(source);
        var i = 0;

        while (i < source.Length)
        {
            // line comment
            if (Matches(source, i, "//"))
            {
                while (i < source.Length && source[i] != '\n')
                {
                    result[i] = ' ';
                    i++;
                }

                continue;
            }

            // block comment
            if (Matches(source, i, "/*"))
            {
                while (i < source.Length && !Matches(source, i, "*/"))
                {
                    if (source[i] != '\n')
                    {
                        result[i] = ' ';
                    }

                    i++;
                }

                for (var k = 0; k < 2 && i < source.Length; k++, i++)
                {
                    result[i] = ' ';
                }

                continue;
            }

            // verbatim string, interpolated or not
            if (Matches(source, i, "@\"") || Matches(source, i, "$@\"") || Matches(source, i, "@$\""))
            {
                var quote = source.IndexOf('"', i);
                result[quote] = ' ';
                i = quote + 1;

                while (i < source.Length)
                {
                    if (source[i] == '"' && Matches(source, i, "\"\""))
                    {
                        result[i] = ' ';
                        result[i + 1] = ' ';
                        i += 2;
                        continue;
                    }

                    if (source[i] == '"')
                    {
                        result[i] = ' ';
                        i++;
                        break;
                    }

                    if (source[i] != '\n')
                    {
                        result[i] = ' ';
                    }

                    i++;
                }

                continue;
            }

            // regular string or char literal
            if (source[i] == '"' || source[i] == '\'')
            {
                var terminator = source[i];
                result[i] = ' ';
                i++;

                while (i < source.Length && source[i] != terminator)
                {
                    if (source[i] == '\\' && i + 1 < source.Length)
                    {
                        result[i] = ' ';
                        result[i + 1] = ' ';
                        i += 2;
                        continue;
                    }

                    if (source[i] == '\n')
                    {
                        break;
                    }

                    result[i] = ' ';
                    i++;
                }

                if (i < source.Length && source[i] == terminator)
                {
                    result[i] = ' ';
                    i++;
                }

                continue;
            }

            i++;
        }

        return result.ToString();
    }

    private static bool Matches(string source, int index, string token) =>
        index + token.Length <= source.Length
        && string.CompareOrdinal(source, index, token, 0, token.Length) == 0;

    private static string Relative(string repoRoot, string path) =>
        path.StartsWith(repoRoot, StringComparison.Ordinal)
            ? path.Substring(repoRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : path;
}
