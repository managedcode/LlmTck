using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Configuration;

public sealed class LlmTckConfigurationBuilder
{
    private LlmTckConfiguration _configuration = LlmTckConfiguration.CreateDefault();

    public LlmTckConfigurationBuilder AddModel(string id, LlmTckModelKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _configuration.Models.RemoveAll(model =>
            string.Equals(model.Id, id, StringComparison.OrdinalIgnoreCase)
        );
        _configuration.Models.Add(new LlmTckModel { Id = id, Kind = kind });

        return this;
    }

    public LlmTckConfigurationBuilder RequireBearerToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        _configuration = _configuration with { RequiredBearerToken = token };
        return this;
    }

    public LlmTckConfigurationBuilder AddChatScenario(
        string id,
        Action<LlmTckScenarioBuilder> configure
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new LlmTckScenarioBuilder(id);
        configure(builder);
        var scenario = builder.Build();

        _configuration.ChatScenarios.RemoveAll(existing =>
            string.Equals(existing.Id, id, StringComparison.OrdinalIgnoreCase)
        );
        _configuration.ChatScenarios.Add(scenario);

        return this;
    }

    public LlmTckConfigurationBuilder WithDefaultEmbeddingVector(params float[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
        {
            throw new ArgumentException("At least one vector value is required.", nameof(values));
        }

        _configuration.DefaultEmbeddingVector.Clear();
        _configuration.DefaultEmbeddingVector.AddRange(values);

        return this;
    }

    public LlmTckConfigurationBuilder WithDefaultAudio(byte[] bytes, string mediaType)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        if (bytes.Length == 0)
        {
            throw new ArgumentException("At least one audio byte is required.", nameof(bytes));
        }

        _configuration = _configuration with
        {
            DefaultAudioBytes = [.. bytes],
            DefaultAudioMediaType = mediaType,
        };

        return this;
    }

    public LlmTckConfiguration Build()
    {
        return Snapshot(_configuration);
    }

    internal static LlmTckConfiguration Snapshot(LlmTckConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration with
        {
            Models = [.. configuration.Models],
            ChatScenarios = [.. configuration.ChatScenarios.Select(SnapshotScenario)],
            DefaultEmbeddingVector = [.. configuration.DefaultEmbeddingVector],
            DefaultAudioBytes = [.. configuration.DefaultAudioBytes],
        };
    }

    private static LlmTckScenario SnapshotScenario(LlmTckScenario scenario)
    {
        return scenario with
        {
            Match = scenario.Match with { Messages = [.. scenario.Match.Messages] },
            Responses =
            [
                .. scenario.Responses.Select(response => response with
                {
                    StreamChunks = [.. response.StreamChunks],
                }),
            ],
        };
    }
}
