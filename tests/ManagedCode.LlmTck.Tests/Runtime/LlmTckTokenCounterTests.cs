using ManagedCode.LlmTck.Runtime;

namespace ManagedCode.LlmTck.Tests.Runtime;

public sealed class LlmTckTokenCounterTests
{
    [Test]
    [Arguments("hello,world", 3)]
    [Arguments("Text tokenization is the process of splitting a string into a list of tokens.", 16)]
    [Arguments("antidisestablishmentarianism", 6)]
    public async Task CountTextTokens_UsesTiktokenO200kBaseCountsAsync(
        string text,
        int expectedTokens
    )
    {
        await Assert.That(LlmTckTokenCounter.CountTextTokens(text)).IsEqualTo(expectedTokens);
    }

    [Test]
    public async Task CountTextTokens_ReturnsZeroForMissingTextAsync()
    {
        await Assert.That(LlmTckTokenCounter.CountTextTokens(null)).IsEqualTo(0);
        await Assert.That(LlmTckTokenCounter.CountTextTokens(string.Empty)).IsEqualTo(0);
    }
}
