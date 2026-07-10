namespace ManagedCode.LlmTck.Runtime;

/// <summary>
/// Optional fast lookup surface for hosts that correlate a completed request with its runtime
/// outcome without materializing the aggregate assertion summary.
/// </summary>
public interface ILlmTckRuntimeEventLookup
{
    bool TryGetEvent(string requestId, out LlmTckRuntimeEvent? runtimeEvent);
}
