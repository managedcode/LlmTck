using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Runtime;

namespace ManagedCode.LlmTck.Tests.Configuration;

public sealed class TranscriptionUsageConfigurationTests
{
    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    [Arguments(double.NaN)]
    [Arguments(double.PositiveInfinity)]
    public async Task Duration_RejectsNonpositiveOrNonfiniteUsageAsync(double seconds)
    {
        await Assert.That(() => LlmTckTranscriptionUsage.ForDuration(seconds)).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(-1, 0, 1)]
    [Arguments(1, -1, 1)]
    [Arguments(1, 0, -1)]
    [Arguments(0, 0, 0)]
    [Arguments(long.MaxValue, 1, 1)]
    public async Task Tokens_RejectsNegativeEmptyOrOverflowUsageAsync(long audio, long text, long output)
    {
        await Assert.That(() => LlmTckTranscriptionUsage.ForTokens(audio, text, output)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Configuration_RejectsIncompleteOrMixedUsageAsync()
    {
        await Assert.That(() => new LlmTckConfigurationBuilder()
            .WithDefaultTranscriptionUsage(new() { InputAudioTokens = 12 })).Throws<ArgumentException>();
        await Assert.That(() => new LlmTckConfigurationBuilder()
            .WithDefaultTranscriptionUsage(new() { DurationSeconds = null, InputAudioTokens = 12, OutputTokens = 1 }))
            .Throws<ArgumentException>();
        await Assert.That(() => new LlmTckConfigurationBuilder().WithDefaultTranscriptionUsage(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new LlmTckRuntime(new LlmTckConfiguration { DefaultTranscriptionUsage = null! }))
            .Throws<ArgumentNullException>();
    }
}
