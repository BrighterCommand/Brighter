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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.GeneratedFileAudit;

/// <summary>
/// A scan over a tree passes trivially when it finds nothing to look at, so the audit that guards
/// the repository has to be shown catching something. These canaries generate a small tree with
/// the real generators, then break it in each of the two ways the audit exists to notice.
/// </summary>
/// <remarks>
/// Each case runs against both the singular and the plural configuration forms. The singular form
/// is the one with the history: dropping the Sync call from its branch of the outbox generator is
/// what orphaned 37 files, and seven checked-in projects go through it. It is also the form whose
/// empty prefix collapses <c>MessagingGateway/{prefix}/Generated</c> to <c>MessagingGateway/Generated</c>,
/// so the audit's path model has to agree with the generator's about a segment that is not there.
/// </remarks>
public class AuditCanaryTests : IDisposable
{
    // The plural form - MSSQL, Kafka, AWS and the rest - whose destination folder is the entry key.
    private const string PLURAL_CONFIGURATION =
        """
        {
          "Namespace": "Sample.Tests",
          "MessagingGateways": {
            "Sample": {
              "Publication": "Sample.SamplePublication",
              "Subscription": "Sample.SampleSubscription",
              "MessageGatewayProvider": "Sample.Tests.SampleMessageGatewayProvider",
              "Category": "Sample",
              "CollectionName": "Sample"
            }
          },
          "Outboxes": {
            "Sample": {
              "Transaction": "System.Data.Common.DbTransaction",
              "OutboxProvider": "SampleOutboxProvider",
              "Category": "Sample",
              "CollectionName": "SampleOutbox"
            }
          }
        }
        """;

    // The singular form - DynamoDB, MongoDb, PostgresSQL, Redis and the rest - which has no key and
    // so no folder segment of its own.
    private const string SINGULAR_CONFIGURATION =
        """
        {
          "Namespace": "Sample.Tests",
          "MessagingGateway": {
            "Publication": "Sample.SamplePublication",
            "Subscription": "Sample.SampleSubscription",
            "MessageGatewayProvider": "Sample.Tests.SampleMessageGatewayProvider",
            "Category": "Sample",
            "CollectionName": "Sample"
          },
          "Outbox": {
            "Transaction": "System.Data.Common.DbTransaction",
            "OutboxProvider": "SampleOutboxProvider",
            "Category": "Sample",
            "CollectionName": "SampleOutbox"
          }
        }
        """;

    private readonly string _root;
    private readonly string _testsRoot;
    private readonly string _projectFolder;

    public AuditCanaryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"GeneratedFileAudit_{Guid.NewGuid()}");
        _testsRoot = Path.Combine(_root, "tests");
        _projectFolder = Path.Combine(_testsRoot, "Sample.Tests");
        Directory.CreateDirectory(_projectFolder);
    }

    [Theory]
    [InlineData(PLURAL_CONFIGURATION, "Sample")]
    [InlineData(SINGULAR_CONFIGURATION, "")]
    public async Task When_a_file_the_generator_would_not_write_is_planted_should_report_an_orphan(
        string configuration, string gatewayFolder)
    {
        // Arrange - a tree the generators have just written, which the audit agrees with
        await GenerateAsync(configuration);
        Assert.Empty(GeneratedTreeAudit.Of(_testsRoot).Orphans);

        // Act - a file no template produces, in a directory the generator owns
        var planted = Path.Combine(_projectFolder, "MessagingGateway", gatewayFolder, "Generated",
            "Reactor", "When_no_template_produces_this_should_be_reported.cs");
        File.WriteAllText(planted, "// hand-written, under an <auto-generated> roof");
        var audit = GeneratedTreeAudit.Of(_testsRoot);

        // Assert - the planted file, and only it, is reported
        Assert.Equal([planted], audit.Orphans);
        Assert.Empty(audit.Missing);
    }

    [Theory]
    [InlineData(PLURAL_CONFIGURATION, "Sample")]
    [InlineData(SINGULAR_CONFIGURATION, "")]
    public async Task When_a_file_the_generator_would_write_is_deleted_should_report_it_missing(
        string configuration, string outboxFolder)
    {
        // Arrange - a tree the generators have just written, which the audit agrees with
        await GenerateAsync(configuration);
        Assert.Empty(GeneratedTreeAudit.Of(_testsRoot).Missing);

        // Act - one generated file goes away, as a dropped generation code path would leave it.
        // The Sync suite is the one #4300 dropped from the singular branch.
        var deleted = Directory
            .EnumerateFiles(Path.Combine(_projectFolder, "Outbox", outboxFolder, "Generated", "Sync"), "*.cs")
            .OrderBy(file => file, StringComparer.Ordinal)
            .First();
        File.Delete(deleted);
        var audit = GeneratedTreeAudit.Of(_testsRoot);

        // Assert - the deleted file, and only it, is reported
        Assert.Equal([deleted], audit.Missing);
        Assert.Empty(audit.Orphans);
    }

    [Theory]
    [InlineData(PLURAL_CONFIGURATION)]
    [InlineData(SINGULAR_CONFIGURATION)]
    public async Task When_a_capability_flag_is_turned_off_should_report_the_skipped_files_as_orphans(
        string configuration)
    {
        // Arrange - a tree generated while the outbox still supported transactions
        await GenerateAsync(configuration);
        Assert.Empty(GeneratedTreeAudit.Of(_testsRoot).Orphans);

        // Act - the flag goes off, which is the case #4305 is about: the generator writes files and
        // never removes one, so the templates it now skips stay on disk
        WriteConfiguration(WithoutTransactionSupport(configuration));
        var audit = GeneratedTreeAudit.Of(_testsRoot);

        // Assert - the transaction tests are reported, and nothing is thought missing
        Assert.NotEmpty(audit.Orphans);
        Assert.All(audit.Orphans, orphan =>
            Assert.Contains("Transaction", Path.GetFileName(orphan), StringComparison.InvariantCultureIgnoreCase));
        Assert.Empty(audit.Missing);
    }

    [Theory]
    [InlineData(PLURAL_CONFIGURATION)]
    [InlineData(SINGULAR_CONFIGURATION)]
    public async Task When_a_capability_flag_is_turned_on_should_report_the_new_files_as_missing(
        string configuration)
    {
        // Arrange - a tree generated while the gateway had no publish-confirmation support, so
        // the confirming_posting templates were skipped. That flag is chosen because it is the
        // only rule matching that substring: the dead-letter template's name also carries
        // "requeuing", so turning its own flag on would leave it skipped by a second rule.
        await GenerateAsync(configuration);
        Assert.Empty(GeneratedTreeAudit.Of(_testsRoot).Missing);

        // Act - the flag goes on. This is the other half of the same seam, and it is the direction
        // that runs the ignore predicate through Plan rather than through GenerateAsync.
        WriteConfiguration(WithPublishConfirmationSupport(configuration));
        var audit = GeneratedTreeAudit.Of(_testsRoot);

        // Assert - the newly expected tests are reported, and nothing on disk is orphaned
        Assert.NotEmpty(audit.Missing);
        Assert.All(audit.Missing, file =>
            Assert.Contains("confirming_posting", Path.GetFileName(file), StringComparison.Ordinal));
        Assert.Empty(audit.Orphans);
    }

    [Theory]
    [InlineData(PLURAL_CONFIGURATION)]
    [InlineData(SINGULAR_CONFIGURATION)]
    public async Task When_a_configuration_is_deleted_should_report_its_whole_tree_as_orphans(
        string configuration)
    {
        // Arrange - a tree the generators have just written, which the audit agrees with
        await GenerateAsync(configuration);
        var generated = GeneratedTreeAudit.Of(_testsRoot).OnDisk;
        Assert.NotEmpty(generated);

        // Act - the configuration goes away. GeneratedFilesUnder walks the whole tree rather than
        // only the projects that carry a configuration precisely so that this is reported, and
        // until now that reasoning lived only in a comment.
        File.Delete(Path.Combine(_projectFolder, TestConfigurationLoader.ConfigurationFileName));
        var audit = GeneratedTreeAudit.Of(_testsRoot);

        // Assert - everything the generator had written is now owned by nothing
        Assert.Empty(audit.Expected);
        Assert.Equal(generated.OrderBy(file => file, StringComparer.Ordinal), audit.Orphans);
        Assert.Empty(audit.Missing);
    }

    // The outbox's own flag, which gates every template whose name carries "Transaction".
    private static string WithoutTransactionSupport(string configurationJson) =>
        configurationJson.Replace(
            "\"OutboxProvider\": \"SampleOutboxProvider\",",
            "\"OutboxProvider\": \"SampleOutboxProvider\", \"SupportsTransactions\": false,",
            StringComparison.Ordinal);

    // The gateway flag defaults to false, so turning it on adds templates rather than removing them.
    private static string WithPublishConfirmationSupport(string configurationJson) =>
        configurationJson.Replace(
            "\"MessageGatewayProvider\": \"Sample.Tests.SampleMessageGatewayProvider\",",
            "\"MessageGatewayProvider\": \"Sample.Tests.SampleMessageGatewayProvider\", \"HasSupportToPublishConfirmation\": true,",
            StringComparison.Ordinal);

    private string WriteConfiguration(string configurationJson)
    {
        var configurationFile = Path.Combine(
            _projectFolder, TestConfigurationLoader.ConfigurationFileName);
        File.WriteAllText(configurationFile, configurationJson);
        return configurationFile;
    }

    /// <summary>
    /// Writes the configuration into the sample project and runs the real generators over it, so
    /// that a clean audit afterwards is evidence the expected set agrees with what was written.
    /// </summary>
    /// <param name="configurationJson">The test-configuration.json contents to generate from.</param>
    private async Task GenerateAsync(string configurationJson)
    {
        var configurationFile = WriteConfiguration(configurationJson);

        // Read back through the loader the audit uses, so the canary generates from exactly the
        // configuration the audit will later expect files from.
        var configuration = TestConfigurationLoader.Load(
            configurationFile, defaultDestinationFolder: _projectFolder)!;

        await new Generators.OutboxGenerator(NullLogger<Generators.OutboxGenerator>.Instance)
            .GenerateAsync(configuration);
        await new Generators.MessagingGatewayGenerator(NullLogger<Generators.MessagingGatewayGenerator>.Instance)
            .GenerateAsync(configuration);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
