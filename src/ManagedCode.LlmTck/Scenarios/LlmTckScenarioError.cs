namespace ManagedCode.LlmTck.Scenarios;

public sealed record LlmTckScenarioError
{
    public int StatusCode { get; init; } = 500;

    public string Code { get; init; } = "llm_tck_error";

    public string Message { get; init; } = "Scripted LLM TCK error.";
}
