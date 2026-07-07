using ManagedCode.LlmTck.Anthropic;
using ManagedCode.LlmTck.AzureOpenAI;
using ManagedCode.LlmTck.Bedrock;
using ManagedCode.LlmTck.Cohere;
using ManagedCode.LlmTck.DeepSeek;
using ManagedCode.LlmTck.Foundry;
using ManagedCode.LlmTck.Gemini;
using ManagedCode.LlmTck.Groq;
using ManagedCode.LlmTck.Mistral;
using ManagedCode.LlmTck.Ollama;
using ManagedCode.LlmTck.OpenAI;
using ManagedCode.LlmTck.OpenRouter;
using ManagedCode.LlmTck.Perplexity;
using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Tests.Providers;

public sealed class ProviderPackageCatalogTests
{
    [Test]
    public async Task ProviderPackages_ExposeExpectedCompatibilityProfilesAsync()
    {
        LlmTckProviderProfile[] profiles =
        [
            OpenAiCompatibility.Profile,
            AzureOpenAiCompatibility.Profile,
            MicrosoftFoundryCompatibility.Profile,
            AnthropicCompatibility.Profile,
            GeminiCompatibility.Profile,
            GroqCompatibility.Profile,
            MistralCompatibility.Profile,
            OllamaCompatibility.Profile,
            CohereCompatibility.Profile,
            BedrockCompatibility.Profile,
            OpenRouterCompatibility.Profile,
            DeepSeekCompatibility.Profile,
            PerplexityCompatibility.Profile,
        ];

        await Assert.That(profiles.Select(profile => profile.Id))
            .IsEquivalentTo(
                [
                    "openai",
                    "azure-openai",
                    "microsoft-foundry",
                    "anthropic",
                    "gemini",
                    "groq",
                    "mistral",
                    "ollama",
                    "cohere",
                    "bedrock",
                    "openrouter",
                    "deepseek",
                    "perplexity",
                ]
            );
        await Assert.That(profiles.All(profile => !string.IsNullOrWhiteSpace(profile.DisplayName)))
            .IsTrue();
        await Assert.That(
                profiles.All(profile => !string.IsNullOrWhiteSpace(profile.DefaultEndpointPath))
            )
            .IsTrue();
        await Assert.That(
                profiles.All(profile => profile.Capabilities.Contains(LlmTckProviderCapability.Chat))
            )
            .IsTrue();
        await Assert.That(
                profiles
                    .Where(profile =>
                        profile.Protocol
                            is LlmTckProtocolFamily.OpenAI
                                or LlmTckProtocolFamily.AzureOpenAI
                                or LlmTckProtocolFamily.MicrosoftFoundry
                                or LlmTckProtocolFamily.Gemini
                                or LlmTckProtocolFamily.Groq
                                or LlmTckProtocolFamily.Mistral
                                or LlmTckProtocolFamily.Ollama
                                or LlmTckProtocolFamily.OpenRouter
                                or LlmTckProtocolFamily.DeepSeek
                                or LlmTckProtocolFamily.Perplexity
                    )
                    .All(profile => profile.CompatibilityTags.Contains("openai-compatible"))
            )
            .IsTrue();
    }
}
