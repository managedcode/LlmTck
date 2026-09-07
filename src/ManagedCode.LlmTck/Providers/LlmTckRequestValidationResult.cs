namespace ManagedCode.LlmTck.Providers;

/// <summary>Provider-neutral result of validating a provider request before mapping it.</summary>
public sealed record LlmTckRequestValidationResult(string? Error = null)
{
    public bool IsValid => Error is null;
}
