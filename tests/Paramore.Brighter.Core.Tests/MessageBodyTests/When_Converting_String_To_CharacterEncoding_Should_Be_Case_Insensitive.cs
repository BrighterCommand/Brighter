using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.Core.Tests.MessageBodyTests;

public class CharacterEncodingCaseInsensitiveTests
{
    [Test]
    [Arguments("utf-8")]
    [Arguments("UTF-8")]
    [Arguments("Utf-8")]
    public async Task When_converting_utf8_variants_should_return_utf8(string input)
    {
        var result = input.ToCharacterEncoding();

        await Assert.That(result).IsEqualTo(CharacterEncoding.UTF8);
    }

    [Test]
    [Arguments("us-ascii")]
    [Arguments("US-ASCII")]
    [Arguments("Us-Ascii")]
    public async Task When_converting_ascii_variants_should_return_ascii(string input)
    {
        var result = input.ToCharacterEncoding();

        await Assert.That(result).IsEqualTo(CharacterEncoding.ASCII);
    }

    [Test]
    [Arguments("base64")]
    [Arguments("BASE64")]
    [Arguments("Base64")]
    public async Task When_converting_base64_variants_should_return_base64(string input)
    {
        var result = input.ToCharacterEncoding();

        await Assert.That(result).IsEqualTo(CharacterEncoding.Base64);
    }

    [Test]
    [Arguments("unknown")]
    [Arguments("UNKNOWN")]
    [Arguments("something-else")]
    public async Task When_converting_unknown_string_should_return_raw(string input)
    {
        var result = input.ToCharacterEncoding();

        await Assert.That(result).IsEqualTo(CharacterEncoding.Raw);
    }
}
