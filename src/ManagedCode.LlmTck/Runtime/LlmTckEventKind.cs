namespace ManagedCode.LlmTck.Runtime;

public enum LlmTckEventKind
{
    Matched,
    Unmatched,
    ModelNotFound,
    AuthFailed,
    ScenarioExhausted,
    ErrorReturned,
}
