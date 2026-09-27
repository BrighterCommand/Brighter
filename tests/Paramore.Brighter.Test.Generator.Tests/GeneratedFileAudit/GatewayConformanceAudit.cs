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
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Tests.GeneratedFileAudit;

/// <summary>
/// One <c>GatewayConformance.SubscriptionType</c> a project's configuration declares, plus its
/// <c>AdditionalExpectedSubjects</c> - carried alongside so a caller cannot silently drop them, even
/// though <see cref="GatewayConformanceAudit"/> never counts them towards naming a gateway.
/// </summary>
/// <param name="SubscriptionType">
/// The fully-qualified type name whose containing namespace is compared against a gateway's
/// directory name.
/// </param>
/// <param name="AdditionalExpectedSubjects">
/// Further fully-qualified type names the same configuration expects the sweep to report. These
/// never count as naming a gateway - only <see cref="SubscriptionType"/> does.
/// </param>
public sealed record GatewayConfigurationEntry(
    string SubscriptionType,
    IReadOnlyList<string> AdditionalExpectedSubjects)
{
    public GatewayConfigurationEntry(string subscriptionType) : this(subscriptionType, Array.Empty<string>())
    {
    }
}

/// <summary>
/// Finds the gateways a set of <c>GatewayConformance</c> configurations names zero times, or more
/// than once - the thirteenth-gateway audit (ADR 0073 step 5). A gateway named zero times ships with
/// no conformance sweep at all; one named twice has two configurations disagreeing about which type
/// declares it.
/// </summary>
/// <remarks>
/// A pure predicate over the names it is handed - a gateway is a namespace, named by a
/// <see cref="GatewayConfigurationEntry.SubscriptionType"/> whose containing namespace equals it
/// exactly. The comparison is exact equality, not a prefix match: under a prefix comparison,
/// <c>AWSSQS.V4</c>'s configuration would also name <c>AWSSQS</c>. Reading the real repository tree
/// is a separate, thin concern - see <see cref="OfRepository"/>.
/// </remarks>
public sealed class GatewayConformanceAudit
{
    private GatewayConformanceAudit(
        IReadOnlyList<string> auditedGateways,
        IReadOnlyList<string> unconfigured,
        IReadOnlyList<string> configuredMoreThanOnce)
    {
        AuditedGateways = auditedGateways;
        Unconfigured = unconfigured;
        ConfiguredMoreThanOnce = configuredMoreThanOnce;
    }

    /// <summary>
    /// Every gateway the audit considered, identified by namespace. Pins that a real set of
    /// gateways was read - <see cref="Unconfigured"/> and <see cref="ConfiguredMoreThanOnce"/> being
    /// empty is the passing case, so neither can be used to tell "nothing to report" apart from
    /// "nothing was audited".
    /// </summary>
    public IReadOnlyList<string> AuditedGateways { get; }

    /// <summary>
    /// Gateways no configuration's <see cref="GatewayConfigurationEntry.SubscriptionType"/> names.
    /// </summary>
    public IReadOnlyList<string> Unconfigured { get; }

    /// <summary>
    /// Gateways more than one configuration's <see cref="GatewayConfigurationEntry.SubscriptionType"/>
    /// names.
    /// </summary>
    public IReadOnlyList<string> ConfiguredMoreThanOnce { get; }

    /// <summary>
    /// Audits <paramref name="gatewayNamespaces"/> against <paramref name="configuredSubjects"/>.
    /// </summary>
    /// <param name="gatewayNamespaces">The gateways to audit, identified by their namespace.</param>
    /// <param name="configuredSubjects">Every project's configured <c>GatewayConformance</c> entry.</param>
    /// <returns>The audit.</returns>
    public static GatewayConformanceAudit Of(
        IEnumerable<string> gatewayNamespaces, IEnumerable<GatewayConfigurationEntry> configuredSubjects)
    {
        var namingCount = gatewayNamespaces
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(gateway => gateway, _ => 0, StringComparer.Ordinal);

        foreach (var entry in configuredSubjects)
        {
            var namedGateway = ContainingNamespace(entry.SubscriptionType);
            if (namingCount.ContainsKey(namedGateway))
            {
                namingCount[namedGateway]++;
            }
        }

        var auditedGateways = Sorted(namingCount.Keys);
        var unconfigured = Sorted(namingCount.Where(count => count.Value == 0).Select(count => count.Key));
        var configuredMoreThanOnce =
            Sorted(namingCount.Where(count => count.Value > 1).Select(count => count.Key));

        return new GatewayConformanceAudit(auditedGateways, unconfigured, configuredMoreThanOnce);
    }

    /// <summary>
    /// Audits every shipped gateway under <c>src/</c> against every project's
    /// <c>test-configuration.json</c> under <paramref name="testsRoot"/>.
    /// </summary>
    /// <remarks>
    /// The thin loader: it only turns the repository tree into the names <see cref="Of"/> takes, by
    /// the same <see cref="TestConfigurationLoader"/> the generator and <see cref="GeneratedTreeAudit"/>
    /// already read a configuration by, and states nothing of its own about what "named" means.
    /// </remarks>
    /// <param name="testsRoot">
    /// The repository's <c>tests</c> folder - see <see cref="GeneratedTreeAudit.LocateTestsRoot"/>.
    /// </param>
    /// <returns>The audit of the real repository tree.</returns>
    public static GatewayConformanceAudit OfRepository(string testsRoot)
    {
        var repositoryRoot = Path.GetDirectoryName(testsRoot)
            ?? throw new InvalidOperationException(
                $"Could not determine the repository root from tests folder {testsRoot}.");
        var srcRoot = Path.Combine(repositoryRoot, "src");

        var gatewayNamespaces = Directory
            .EnumerateDirectories(srcRoot, "Paramore.Brighter.MessagingGateway.*")
            .Select(Path.GetFileName)!;

        var configuredSubjects = Directory.EnumerateDirectories(testsRoot)
            .Select(projectFolder => (
                projectFolder,
                configurationFile: Path.Combine(
                    projectFolder, TestConfigurationLoader.ConfigurationFileName)))
            .Where(project => File.Exists(project.configurationFile))
            .Select(project => TestConfigurationLoader.Load(
                project.configurationFile, defaultDestinationFolder: project.projectFolder))
            .Select(configuration => configuration?.GatewayConformance)
            .Where(gatewayConformance => gatewayConformance != null)
            .Select(gatewayConformance => new GatewayConfigurationEntry(
                gatewayConformance!.SubscriptionType, gatewayConformance.AdditionalExpectedSubjects));

        return Of(gatewayNamespaces!, configuredSubjects);
    }

    private static string ContainingNamespace(string fullyQualifiedTypeName)
    {
        var lastDot = fullyQualifiedTypeName.LastIndexOf('.');
        return lastDot < 0 ? string.Empty : fullyQualifiedTypeName[..lastDot];
    }

    private static IReadOnlyList<string> Sorted(IEnumerable<string> values) =>
        values.OrderBy(value => value, StringComparer.Ordinal).ToList();
}
