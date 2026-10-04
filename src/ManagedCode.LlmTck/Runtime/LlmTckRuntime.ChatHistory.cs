using System.Security.Cryptography;
using System.Text;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Runtime;

public sealed partial class LlmTckRuntime
{
    private readonly Dictionary<string, StoredChatResponse> _chatResponses = new(StringComparer.Ordinal);
    private long _chatResponseBytes;

    private LlmTckChatResult? RestoreChatResponse(ref LlmTckChatRequest request, string? token)
    {
        if (!request.StoreResponse && request.PreviousResponseId is null) { return null; }
        if (string.IsNullOrWhiteSpace(request.HistoryNamespace))
        {
            return LlmTckChatResult.Failure(request.ModelId, 400, "invalid_history_namespace", "Response history requires an exact namespace.");
        }
        if (request.PreviousResponseId is null) { return null; }
        if (!_chatResponses.TryGetValue(request.PreviousResponseId, out var previous)
            || previous.Namespace != request.HistoryNamespace || previous.Model != request.ModelId
            || previous.TokenHash != TokenHash(token))
        {
            return LlmTckChatResult.Failure(request.ModelId, 404, "response_not_found", "The previous response is unavailable in this scope.");
        }
        request = request with
        {
            Messages = [.. request.Messages.Where(IsInstruction), .. SnapshotHistory(previous.Messages), .. request.Messages.Where(message => !IsInstruction(message))]
        };
        return null;
    }

    private LlmTckChatResult RetainChatResponse(LlmTckChatRequest request, LlmTckChatResult result, string? token)
    {
        if (!request.StoreResponse) { return result; }
        var messages = SnapshotHistory(request.Messages);
        messages.Add(new LlmTckMessage { Role = "assistant", Content = result.Content, ToolCalls = [.. result.ToolCalls] });
        var bytes = HistoryBytes(messages) + Encoding.UTF8.GetByteCount(request.HistoryNamespace!) + Encoding.UTF8.GetByteCount(request.ModelId);
        if (_chatResponses.Count >= _configuration.MaxStoredChatResponses || bytes > _configuration.MaxStoredChatResponseBytes - _chatResponseBytes)
        {
            return LlmTckChatResult.Failure(request.ModelId, 409, "llm_tck_response_capacity_exceeded", "Response history capacity exceeded. Reset the runtime or increase its configured capacity.");
        }
        var id = $"resp_{Guid.NewGuid():N}";
        _chatResponses.Add(id, new StoredChatResponse(request.HistoryNamespace!, request.ModelId, TokenHash(token), messages));
        _chatResponseBytes += bytes;
        return result with { ResponseId = id };
    }

    private static List<LlmTckMessage> SnapshotHistory(IEnumerable<LlmTckMessage> messages)
    {
        return messages.Where(message => !IsInstruction(message)).Select(message => message with { ToolCalls = [.. message.ToolCalls] }).ToList();
    }

    private static bool IsInstruction(LlmTckMessage message)
    {
        return message.Role is "system" or "developer";
    }

    private static string TokenHash(string? token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty)));
    }

    private static long HistoryBytes(IEnumerable<LlmTckMessage> messages)
    {
        return 256L + messages.Sum(message =>
        256L + Encoding.UTF8.GetByteCount(message.Role) + Encoding.UTF8.GetByteCount(message.Content)
        + Encoding.UTF8.GetByteCount(message.ToolCallId ?? string.Empty)
        + message.ToolCalls.Sum(call => 128L + Encoding.UTF8.GetByteCount(call.Id)
            + Encoding.UTF8.GetByteCount(call.Name) + Encoding.UTF8.GetByteCount(call.ArgumentsJson)));
    }

    private sealed record StoredChatResponse(string Namespace, string Model, string TokenHash, List<LlmTckMessage> Messages);
}
