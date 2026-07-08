using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Ollama;

public static class OllamaWireMapper
{
    public static LlmTckChatRequest ToRuntimeRequest(OllamaChatRequest request)
    {
        return new()
        {
            ModelId = request.Model,
            Stream = request.Stream != false,
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

    public static OllamaChatResponse ToChatResponse(LlmTckChatResult result)
    {
        return new()
        {
            Model = result.ModelId,
            Message = new OllamaChatMessageResponse { Content = result.Content },
            Done = true,
            DoneReason = "stop",
            EvalCount = CountTokens(result.Content),
        };
    }

    public static OllamaChatResponse ToChatChunk(
        LlmTckChatResult result,
        string content,
        bool done
    )
    {
        return new()
        {
            Model = result.ModelId,
            Message = new OllamaChatMessageResponse { Content = content },
            Done = done,
            DoneReason = done ? "stop" : null,
            EvalCount = done ? CountTokens(result.Content) : 0,
        };
    }

    public static OllamaEmbedResponse ToEmbedResponse(
        string model,
        IReadOnlyList<IReadOnlyList<float>> vectors
    )
    {
        return new()
        {
            Model = model,
            Embeddings = vectors,
            PromptEvalCount = vectors.Count,
        };
    }

    public static OllamaErrorResponse ToError(string message)
    {
        return new() { Error = message };
    }

    public static List<string> ReadEmbeddingInputs(OllamaEmbedRequest request)
    {
        return request.Input.ValueKind == System.Text.Json.JsonValueKind.Array
            ? request
                .Input
                .EnumerateArray()
                .Select(item => item.ValueKind == System.Text.Json.JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString())
                .ToList()
            : [request.Input.ValueKind == System.Text.Json.JsonValueKind.String ? request.Input.GetString() ?? string.Empty : request.Input.ToString()];
    }

    private static int CountTokens(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? 0
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
