namespace ManagedCode.LlmTck.Runtime;

public interface ILlmTckVideoStore
{
    LlmTckVideoResult StoreVideo(string provider, LlmTckVideoResult video);
    IReadOnlyList<LlmTckVideoResult> ListVideos(string provider);
    LlmTckVideoResult? FindVideo(string provider, string id, bool generation = false);
    bool DeleteVideo(string provider, string id);
}
