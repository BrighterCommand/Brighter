using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Verifies that the generator reads the conformance ledger and emits a Deferred Skip on a
/// canonical test only when the ledger cell is not yet Pass/Fixed. Legacy templates are never
/// given a Skip by the ledger mechanism.
/// </summary>
public class WhenLedgerMarksACellShouldEmitSkipOnlyWhenNotProven : IDisposable
{
    // FR-22 maps to When_requeuing_a_failed_message_should_be_redelivered in the generator's
    // canonical-template→FR-column map.
    private const string CANONICAL_TEMPLATE_NAME =
        "When_requeuing_a_failed_message_should_be_redelivered";

    // Ledger row key that will be set on the test MessagingGatewayConfiguration.
    private const string LEDGER_KEY = "TestTransport / TestConfig";

    // FR column that the canonical template above maps to.
    private const string FR_COLUMN = "FR-22";

    // A non-canonical template name (not in the canonical map) used to verify the mechanism
    // does NOT apply to templates outside the canonical set.
    private const string NON_CANONICAL_TEMPLATE_NAME = "When_non_canonical_template_should_not_get_skip";

    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;
    private readonly string _canonicalTemplatePath;
    private readonly string? _canonicalTemplateOriginalContent;
    private readonly string _nonCanonicalTemplatePath;

    public WhenLedgerMarksACellShouldEmitSkipOnlyWhenNotProven()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"LedgerSkipTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();

        var reactorTemplatesDir = Path.Combine(
            AppContext.BaseDirectory, "Templates", "MessagingGateway", "Reactor");

        // Canonical template that uses the {% if Skip != empty %} pattern; the generator
        // should emit ", Skip = "..."" when the ledger cell is not Pass/Fixed.
        // Back up the original file (the real canonical template) so Dispose can restore it.
        _canonicalTemplatePath = Path.Combine(
            reactorTemplatesDir, $"{CANONICAL_TEMPLATE_NAME}.cs.liquid");
        _canonicalTemplateOriginalContent = File.Exists(_canonicalTemplatePath)
            ? File.ReadAllText(_canonicalTemplatePath)
            : null;
        File.WriteAllText(_canonicalTemplatePath,
            "[Fact{%- if Skip != empty -%}(Skip = \"{{ Skip }}\"){%- endif -%}]\n" +
            $"public void {CANONICAL_TEMPLATE_NAME}() {{ }}\n");

        // Non-canonical placeholder template (not in the FR-column map) — verifies that
        // the Skip mechanism applies only to entries in CanonicalTemplateFrColumns.
        _nonCanonicalTemplatePath = Path.Combine(
            reactorTemplatesDir, $"{NON_CANONICAL_TEMPLATE_NAME}.cs.liquid");
        File.WriteAllText(_nonCanonicalTemplatePath,
            "[Fact{%- if Skip != empty -%}(Skip = \"{{ Skip }}\"){%- endif -%}]\n" +
            $"public void {NON_CANONICAL_TEMPLATE_NAME}() {{ }}\n");
    }

    [Fact]
    public async Task When_ledger_cell_is_pass_should_not_emit_skip()
    {
        // Arrange — ledger says Pass for (LEDGER_KEY, FR-22): test should run without Skip.
        var ledger = new InMemoryConformanceLedger(
            new Dictionary<(string, string), string>
            {
                [(LEDGER_KEY, FR_COLUMN)] = "Pass"
            });

        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the generated canonical test carries NO Skip attribute argument.
        var generatedContent = await ReadGeneratedCanonicalFile(configuration);
        Assert.DoesNotContain("Skip", generatedContent);
    }

    [Fact]
    public async Task When_ledger_cell_is_fixed_should_not_emit_skip()
    {
        // Arrange — ledger says Fixed (#PR42): test should run without Skip.
        var ledger = new InMemoryConformanceLedger(
            new Dictionary<(string, string), string>
            {
                [(LEDGER_KEY, FR_COLUMN)] = "Fixed (#PR42)"
            });

        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var generatedContent = await ReadGeneratedCanonicalFile(configuration);
        Assert.DoesNotContain("Skip", generatedContent);
    }

    [Fact]
    public async Task When_ledger_cell_is_unknown_should_emit_skip_with_placeholder_number()
    {
        // Arrange — ledger cell is Unknown (transient fix-phase state): Skip uses #NNNN.
        var ledger = new InMemoryConformanceLedger(
            new Dictionary<(string, string), string>
            {
                [(LEDGER_KEY, FR_COLUMN)] = "Unknown"
            });

        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the generated test carries Skip = "Deferred: #NNNN — … (maintainer sign-off)".
        var generatedContent = await ReadGeneratedCanonicalFile(configuration);
        Assert.Contains("Skip = \"Deferred: #NNNN", generatedContent);
        Assert.Contains("(maintainer sign-off)", generatedContent);
    }

    [Fact]
    public async Task When_ledger_cell_is_deferred_should_emit_skip_with_real_issue_number()
    {
        // Arrange — ledger cell carries a real issue number: Skip uses #1234, not #NNNN.
        const string REAL_ISSUE_NUMBER = "1234";
        var ledger = new InMemoryConformanceLedger(
            new Dictionary<(string, string), string>
            {
                [(LEDGER_KEY, FR_COLUMN)] = $"Deferred -> #{REAL_ISSUE_NUMBER} (sign-off: @m)"
            });

        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the real issue number appears in the Skip, not the placeholder #NNNN.
        var generatedContent = await ReadGeneratedCanonicalFile(configuration);
        Assert.Contains($"Skip = \"Deferred: #{REAL_ISSUE_NUMBER}", generatedContent);
        Assert.DoesNotContain("#NNNN", generatedContent);
    }

    [Fact]
    public async Task When_template_is_not_canonical_should_not_emit_skip_regardless_of_ledger()
    {
        // Arrange — ledger marks the FR-22 cell Deferred, but the non-canonical template
        // is not in the canonical-template→FR-column map, so it must receive no Skip.
        var ledger = new InMemoryConformanceLedger(
            new Dictionary<(string, string), string>
            {
                [(LEDGER_KEY, FR_COLUMN)] = "Deferred -> #9999 (sign-off: @maintainer)"
            });

        var configuration = BuildConfiguration();
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the non-canonical file exists and contains no Skip.
        var nonCanonicalOutput = Path.Combine(
            _testDirectory, "MessagingGateway", "Test", "Generated", "Reactor",
            $"{NON_CANONICAL_TEMPLATE_NAME}.cs");
        Assert.True(File.Exists(nonCanonicalOutput),
            "Non-canonical template must still be generated");
        var nonCanonicalContent = await File.ReadAllTextAsync(nonCanonicalOutput);
        Assert.DoesNotContain("Skip", nonCanonicalContent);
    }

    [Fact]
    public async Task When_no_ledger_key_is_set_should_not_emit_skip()
    {
        // Arrange — configuration has no LedgerKey; mechanism must treat it as untracked.
        var ledger = new InMemoryConformanceLedger(
            new Dictionary<(string, string), string>
            {
                [(LEDGER_KEY, FR_COLUMN)] = "Unknown"
            });

        // Configuration intentionally omits LedgerKey.
        var configuration = BuildConfiguration(ledgerKey: null);
        var generator = new Generators.MessagingGatewayGenerator(_logger, ledger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — no Skip emitted when LedgerKey is absent.
        var generatedContent = await ReadGeneratedCanonicalFile(configuration);
        Assert.DoesNotContain("Skip", generatedContent);
    }

    [Fact]
    public async Task When_ledger_is_loaded_from_repo_root_unknown_cells_emit_skip()
    {
        // Arrange — no injected ledger; the generator must locate the real
        // conformance-status.md by walking up from AppContext.BaseDirectory.
        // Every cell in the real ledger is Unknown, so the canonical template
        // must carry the Deferred #NNNN Skip.
        var configuration = BuildConfiguration(ledgerKey: "Kafka / Standard");
        var generator = new Generators.MessagingGatewayGenerator(_logger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — the real ledger was found and the Unknown cell produced a Skip.
        var generatedContent = await ReadGeneratedCanonicalFile(configuration);
        Assert.Contains("Skip = \"Deferred: #NNNN", generatedContent);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private TestConfiguration BuildConfiguration(string? ledgerKey = LEDGER_KEY) =>
        new()
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageBuilder = "TestMessageBuilder",
            MessageAssertion = "TestMessageAssertion",
            MessagingGateway = new MessagingGatewayConfiguration
            {
                Prefix = "Test",
                Namespace = "MyApp.Tests",
                MessageGatewayProvider = "TestProvider",
                Publication = "Publication",
                Subscription = "Subscription",
                LedgerKey = ledgerKey,
            }
        };

    private async Task<string> ReadGeneratedCanonicalFile(TestConfiguration configuration)
    {
        var outputPath = Path.Combine(
            _testDirectory, "MessagingGateway", "Test", "Generated", "Reactor",
            $"{CANONICAL_TEMPLATE_NAME}.cs");
        Assert.True(File.Exists(outputPath),
            $"Generated file not found at {outputPath}");
        return await File.ReadAllTextAsync(outputPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);

        // Restore the original canonical template (the real template may have existed before
        // this test overwrote it with a minimal stub). If the file was absent originally,
        // delete the stub so later tests do not run against it.
        if (_canonicalTemplateOriginalContent != null)
            File.WriteAllText(_canonicalTemplatePath, _canonicalTemplateOriginalContent);
        else if (File.Exists(_canonicalTemplatePath))
            File.Delete(_canonicalTemplatePath);

        if (File.Exists(_nonCanonicalTemplatePath))
            File.Delete(_nonCanonicalTemplatePath);
    }
}
