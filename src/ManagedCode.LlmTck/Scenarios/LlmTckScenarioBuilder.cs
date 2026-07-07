namespace ManagedCode.LlmTck.Scenarios;

public sealed class LlmTckScenarioBuilder(string id)
{
    private LlmTckScenario _scenario = new() { Id = id };

    public LlmTckScenarioBuilder ForModel(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        _scenario = _scenario with { ModelId = modelId };
        return this;
    }

    public LlmTckScenarioBuilder RequireBearerToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _scenario = _scenario with { RequiredBearerToken = token };
        return this;
    }

    public LlmTckScenarioBuilder WhenUserContains(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        _scenario.Match.Messages.Add(new LlmTckMessage { Role = "user", Content = content });
        return this;
    }

    public LlmTckScenarioBuilder WithExactMatch(params LlmTckMessage[] messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        _scenario.Match.Messages.Clear();
        _scenario.Match.Messages.AddRange(messages);
        _scenario = _scenario with
        {
            Match = _scenario.Match with { Mode = LlmTckMatchMode.Exact },
        };
        return this;
    }

    public LlmTckScenarioBuilder Responds(string content, params string[] streamChunks)
    {
        ArgumentNullException.ThrowIfNull(content);
        _scenario.Responses.Add(
            new LlmTckScenarioResponse
            {
                Content = content,
                StreamChunks = streamChunks.Length > 0 ? [.. streamChunks] : [content],
            }
        );
        return this;
    }

    public LlmTckScenarioBuilder Fails(int statusCode, string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _scenario.Responses.Add(
            new LlmTckScenarioResponse
            {
                Error = new LlmTckScenarioError
                {
                    StatusCode = statusCode,
                    Code = code,
                    Message = message,
                },
            }
        );
        return this;
    }

    public LlmTckScenarioBuilder DelaysBy(int milliseconds)
    {
        if (milliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds), "Delay cannot be negative.");
        }

        if (_scenario.Responses.Count == 0)
        {
            _scenario.Responses.Add(new LlmTckScenarioResponse());
        }

        var index = _scenario.Responses.Count - 1;
        _scenario.Responses[index] = _scenario.Responses[index] with
        {
            DelayMilliseconds = milliseconds,
        };
        return this;
    }

    public LlmTckScenario Build()
    {
        if (_scenario.Responses.Count == 0)
        {
            throw new InvalidOperationException("A scenario requires at least one response.");
        }

        return _scenario with
        {
            Match = _scenario.Match with { Messages = [.. _scenario.Match.Messages] },
            Responses =
            [
                .. _scenario.Responses.Select(response => response with
                {
                    StreamChunks = [.. response.StreamChunks],
                }),
            ],
        };
    }
}
