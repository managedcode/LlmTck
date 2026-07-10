namespace ManagedCode.LlmTck.Runtime;

/// <summary>
/// Optional provider-neutral correlation scope used by hosts to associate runtime events with an
/// external request without changing the operation methods on <see cref="ILlmTckRuntime"/>.
/// </summary>
public interface ILlmTckRuntimeRequestScope
{
    IDisposable BeginRequest(string requestId);
}
