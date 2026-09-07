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
            DefaultEndpointPath = "/openai/v1",
            Capabilities =
            [
                LlmTckProviderCapability.Models,
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
                LlmTckProviderCapability.Embeddings,
                LlmTckProviderCapability.Images,
                LlmTckProviderCapability.StreamingImages,
                LlmTckProviderCapability.Video,
                LlmTckProviderCapability.Audio,
                LlmTckProviderCapability.StreamingAudio,
            ],
            CompatibilityTags = [ProviderId, LlmTckCompatibilityTags.OpenAICompatible],
            ApiContract = new()
            {
                DocumentationUrl = "https://developers.openai.com/api/reference/",
                DocumentationRetrievedOn = "2026-09-07",
                DocumentationVersion = "v1",
                Operations =
                [
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.ModelsList,
                        Method = "GET",
                        Path = "/openai/v1/models",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/models/methods/list/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Models],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.ChatCompletionsCreate,
                        Method = "POST",
                        Path = "/openai/v1/chat/completions",
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
                        Id = LlmTckProviderOperationIds.OpenAI.ResponsesCreate,
                        Method = "POST",
                        Path = "/openai/v1/responses",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/responses/methods/create/",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities =
                        [
                            LlmTckProviderCapability.Chat,
                            LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
                            LlmTckProviderCapability.Images,
                        ],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.EmbeddingsCreate,
                        Method = "POST",
                        Path = "/openai/v1/embeddings",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/embeddings/methods/create/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.ImagesCreate,
                        Method = "POST",
                        Path = "/openai/v1/images/generations",
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
                        Id = LlmTckProviderOperationIds.OpenAI.ImagesEditsCreate,
                        Method = "POST",
                        Path = "/openai/v1/images/edits",
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
                        Id = LlmTckProviderOperationIds.OpenAI.ImagesVariationsCreate,
                        Method = "POST",
                        Path = "/openai/v1/images/variations",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/images/methods/create_variation/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Images],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.AudioSpeechCreate,
                        Method = "POST",
                        Path = "/openai/v1/audio/speech",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/audio/subresources/speech/methods/create/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.AudioTranscriptionsCreate,
                        Method = "POST",
                        Path = "/openai/v1/audio/transcriptions",
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
                        Id = LlmTckProviderOperationIds.OpenAI.AudioTranslationsCreate,
                        Method = "POST",
                        Path = "/openai/v1/audio/translations",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/audio/subresources/translations/methods/create/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.VideosCreate,
                        Method = "POST",
                        Path = "/openai/v1/videos",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/create/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.VideosList,
                        Method = "GET",
                        Path = "/openai/v1/videos",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/list/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.VideosRetrieve,
                        Method = "GET",
                        Path = "/openai/v1/videos/{videoId}",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/retrieve/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.VideosDelete,
                        Method = "DELETE",
                        Path = "/openai/v1/videos/{videoId}",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/delete/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.VideosContentRetrieve,
                        Method = "GET",
                        Path = "/openai/v1/videos/{videoId}/content",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/download_content/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.VideosEditsCreate,
                        Method = "POST",
                        Path = "/openai/v1/videos/edits",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/edit/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.VideosExtensionsCreate,
                        Method = "POST",
                        Path = "/openai/v1/videos/extensions",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/extend/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.VideosRemix,
                        Method = "POST",
                        Path = "/openai/v1/videos/{videoId}/remix",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/remix/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.VideosCharactersCreate,
                        Method = "POST",
                        Path = "/openai/v1/videos/characters",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/create_character/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenAI.VideosCharactersRetrieve,
                        Method = "GET",
                        Path = "/openai/v1/videos/characters/{characterId}",
                        DocumentationUrl =
                            "https://developers.openai.com/api/reference/resources/videos/methods/get_character/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                ],
            },
        };
}
