using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.OpenAI;

public static class OpenAiCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.OpenAI;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "OpenAI",
            Protocol = LlmTckProtocolFamily.OpenAI,
            DefaultEndpointPath = "/v1",
            Capabilities =
            [
                LlmTckProviderCapability.Models,
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Embeddings,
                LlmTckProviderCapability.Images,
                LlmTckProviderCapability.StreamingImages,
                LlmTckProviderCapability.Video,
                LlmTckProviderCapability.Audio,
                LlmTckProviderCapability.StreamingAudio,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags = [ProviderId, LlmTckCompatibilityTags.OpenAICompatible],
            ApiContract = new()
            {
                DocumentationUrl = "https://developers.openai.com/api/reference/",
                DocumentationRetrievedOn = "2026-07-08",
                DocumentationVersion = "v1",
                Operations =
                [
                    new()
                    {
                        Id = "models.list",
                        Method = "GET",
                        Path = "/v1/models",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/models/methods/list/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Models],
                    },
                    new()
                    {
                        Id = "chat.completions.create",
                        Method = "POST",
                        Path = "/v1/chat/completions",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create/",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities =
                        [
                            LlmTckProviderCapability.Chat,
                            LlmTckProviderCapability.StreamingChat,
                            LlmTckProviderCapability.Tools,
                            LlmTckProviderCapability.StructuredOutput,
                        ],
                    },
                    new()
                    {
                        Id = "responses.create",
                        Method = "POST",
                        Path = "/v1/responses",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/responses/methods/create/",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities =
                        [
                            LlmTckProviderCapability.Chat,
                            LlmTckProviderCapability.StreamingChat,
                            LlmTckProviderCapability.Images,
                            LlmTckProviderCapability.Tools,
                            LlmTckProviderCapability.StructuredOutput,
                        ],
                    },
                    new()
                    {
                        Id = "embeddings.create",
                        Method = "POST",
                        Path = "/v1/embeddings",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/embeddings/methods/create/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                    new()
                    {
                        Id = "images.create",
                        Method = "POST",
                        Path = "/v1/images/generations",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/images/methods/generate/",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities =
                        [
                            LlmTckProviderCapability.Images,
                            LlmTckProviderCapability.StreamingImages,
                        ],
                    },
                    new()
                    {
                        Id = "images.edits.create",
                        Method = "POST",
                        Path = "/v1/images/edits",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/images/methods/edit/",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities =
                        [
                            LlmTckProviderCapability.Images,
                            LlmTckProviderCapability.StreamingImages,
                        ],
                    },
                    new()
                    {
                        Id = "images.variations.create",
                        Method = "POST",
                        Path = "/v1/images/variations",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/images/methods/create_variation/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Images],
                    },
                    new()
                    {
                        Id = "audio.speech.create",
                        Method = "POST",
                        Path = "/v1/audio/speech",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/audio/subresources/speech/methods/create/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = "audio.transcriptions.create",
                        Method = "POST",
                        Path = "/v1/audio/transcriptions",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/audio/subresources/transcriptions/methods/create/",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities =
                        [
                            LlmTckProviderCapability.Audio,
                            LlmTckProviderCapability.StreamingAudio,
                        ],
                    },
                    new()
                    {
                        Id = "audio.translations.create",
                        Method = "POST",
                        Path = "/v1/audio/translations",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/audio/subresources/translations/methods/create/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = "videos.create",
                        Method = "POST",
                        Path = "/v1/videos",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/create/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "videos.list",
                        Method = "GET",
                        Path = "/v1/videos",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/list/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "videos.retrieve",
                        Method = "GET",
                        Path = "/v1/videos/{videoId}",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/retrieve/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "videos.delete",
                        Method = "DELETE",
                        Path = "/v1/videos/{videoId}",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/delete/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "videos.content.retrieve",
                        Method = "GET",
                        Path = "/v1/videos/{videoId}/content",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/download_content/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "videos.edits.create",
                        Method = "POST",
                        Path = "/v1/videos/edits",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/edit/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "videos.extensions.create",
                        Method = "POST",
                        Path = "/v1/videos/extensions",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/extend/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "videos.remix",
                        Method = "POST",
                        Path = "/v1/videos/{videoId}/remix",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/remix/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "videos.characters.create",
                        Method = "POST",
                        Path = "/v1/videos/characters",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/create_character/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "videos.characters.retrieve",
                        Method = "GET",
                        Path = "/v1/videos/characters/{characterId}",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/get_character/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                ],
            },
        };
}
