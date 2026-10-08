using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Decisions;

public enum LlmTckDecisionProvider { Unspecified, TypeSafe, Kev, Cloudflare, OpenAI }

public enum LlmTckDecisionKind { Predicate, Choice, Score }

public sealed record LlmTckDecisionQuestion
{
    public string Name { get; init; } = string.Empty;
    public LlmTckDecisionKind Kind { get; init; }
    public List<string> Options { get; init; } = [];
}

public sealed record LlmTckDecisionRequest
{
    public string ModelId { get; init; } = string.Empty;
    public string Input { get; init; } = string.Empty;
    public List<LlmTckDecisionQuestion> Questions { get; init; } = [];
}

public sealed record LlmTckDecisionAnswer
{
    public LlmTckDecisionKind Kind { get; init; }
    public bool Refused { get; init; }
    public double? Probability { get; init; }
    public string? Choice { get; init; }
    public double? Score { get; init; }
    public double? Confidence { get; init; }
    public Dictionary<string, double> Probabilities { get; init; } = [];
}

public sealed record LlmTckDecisionScenario
{
    public string Id { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string? ExpectedInput { get; init; }
    public Dictionary<string, LlmTckDecisionAnswer> Answers { get; init; } = [];
    public LlmTckTokenUsage Usage { get; init; } = new();
    public LlmTckScenarioError? Error { get; init; }
}

public sealed record LlmTckDecisionResult
{
    public bool IsSuccess { get; init; }
    public string ModelId { get; init; } = string.Empty;
    public string? ScenarioId { get; init; }
    public Dictionary<string, LlmTckDecisionAnswer> Answers { get; init; } = [];
    public LlmTckTokenUsage Usage { get; init; } = new();
    public int StatusCode { get; init; } = 200;
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
