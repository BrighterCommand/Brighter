using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// A Deferred ledger cell records that a behaviour is knowingly not conformant, and the sign-off
/// token names the maintainer who accepted that deferral (ADR 0067, FR-13, AC-24). A placeholder
/// handle satisfies the shape of a sign-off without naming anybody, so it lets a deferral enter
/// the ledger with no accountable owner — the gap the sign-off exists to close.
///
/// These tests specify that the audit accepts a real handle and rejects a placeholder one.
/// </summary>
public class LedgerSignOffHandleTests
{
    [Fact]
    public void When_a_sign_off_is_a_placeholder_handle_should_fail_audit()
    {
        // Arrange — a well-formed cell whose sign-off names nobody
        const string cell = "Deferred -> #4240 (sign-off: @maintainer)";

        // Act
        var isValid = LedgerSkipCrossCheckAudit.IsValidDeferredCell(cell);

        // Assert
        Assert.False(isValid,
            $"Expected '{cell}' to fail validation: '@maintainer' is a placeholder, not a maintainer.");
    }

    [Fact]
    public void When_a_sign_off_is_an_abbreviated_handle_should_fail_audit()
    {
        // Arrange — '@m' is the abbreviation the generator's own fixtures used
        const string cell = "Deferred -> #4240 (sign-off: @m)";

        // Act
        var isValid = LedgerSkipCrossCheckAudit.IsValidDeferredCell(cell);

        // Assert
        Assert.False(isValid,
            $"Expected '{cell}' to fail validation: '@m' is too short to identify a maintainer.");
    }

    [Fact]
    public void When_a_sign_off_is_a_real_handle_should_pass_audit()
    {
        // Arrange — the handle every Deferred cell in the live ledger actually carries
        const string cell = "Deferred -> #4240 (sign-off: @iancooper)";

        // Act
        var isValid = LedgerSkipCrossCheckAudit.IsValidDeferredCell(cell);

        // Assert
        Assert.True(isValid,
            $"Expected '{cell}' to pass validation: '@iancooper' is a real handle.");
    }
}
