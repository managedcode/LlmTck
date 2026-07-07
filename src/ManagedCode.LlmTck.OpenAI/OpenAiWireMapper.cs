using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.OpenAI;

public static class OpenAiWireMapper
{
    public static LlmTckChatRequest ToRuntimeRequest(OpenAiChatCompletionRequest request)
    {
        return new()
        {
            ModelId = request.Model,
            Stream = request.Stream,
            Messages = request
                .Messages
                .Select(message => new LlmTckMessage
                {
                    Role = message.Role,
                    Content = message.TextContent,
                })
                .ToList(),
        };
    }

    public static OpenAiChatCompletionResponse ToChatResponse(LlmTckChatResult result)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return new OpenAiChatCompletionResponse
        {
            Id = CreateResponseId(),
            Created = now,
            Model = result.ModelId,
            Choices =
            [
                new OpenAiChatChoice
                {
                    Index = 0,
                    Message = OpenAiChatMessage.FromText("assistant", result.Content),
                },
            ],
            Usage = CreateUsage(0, result.Content),
        };
    }

    public static OpenAiChatCompletionChunk ToChatChunk(
        LlmTckChatResult result,
        string content,
        string responseId,
        long created,
        string? finishReason = null
    )
    {
        return new()
        {
            Id = responseId,
            Created = created,
            Model = result.ModelId,
            Choices =
            [
                new OpenAiChatChunkChoice
                {
                    Index = 0,
                    Delta = OpenAiChatMessage.FromText("assistant", content),
                    FinishReason = finishReason,
                },
            ],
        };
    }

    public static object ToModelsResponse(IEnumerable<LlmTckModel> models)
    {
        return new
        {
            @object = "list",
            data = models.Select(model => new
            {
                id = model.Id,
                @object = "model",
                owned_by = model.OwnedBy,
                metadata = new { kind = model.Kind.ToString().ToLowerInvariant() },
            }),
        };
    }

    public static OpenAiEmbeddingResponse ToEmbeddingResponse(
        string model,
        IReadOnlyList<IReadOnlyList<float>> vectors,
        bool encodeAsBase64 = false
    )
    {
        return new()
        {
            Id = CreateResponseId(),
            Model = model,
            Data = vectors
                .Select((vector, index) => OpenAiEmbeddingData.FromVector(index, vector, encodeAsBase64))
                .ToList(),
            Usage = new OpenAiUsage
            {
                PromptTokens = vectors.Count,
                TotalTokens = vectors.Count,
            },
        };
    }

    public static OpenAiImageGenerationResponse ToImageResponse(string dataUri)
    {
        var commaIndex = dataUri.IndexOf(',', StringComparison.Ordinal);
        var base64 = commaIndex >= 0 ? dataUri[(commaIndex + 1)..] : dataUri;

        return new OpenAiImageGenerationResponse
        {
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Data = [new OpenAiImageData { Base64Json = base64 }],
        };
    }

    public static OpenAiErrorResponse ToError(string code, string message)
    {
        return new()
        {
            Error = new OpenAiError
            {
                Code = code,
                Message = message,
            },
        };
    }

    public static string CreateResponseId()
    {
        return $"llmtck-{Guid.NewGuid():N}";
    }

    private static OpenAiUsage CreateUsage(int promptTokens, string completion)
    {
        var completionTokens = string.IsNullOrWhiteSpace(completion)
            ? 0
            : completion.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return new()
        {
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens,
        };
    }
}
