using ManagedCode.LlmTck.Aspire;
using ManagedCode.LlmTck.Providers;

var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddLlmTck(
        LlmTckCompatibilityTags.OpenAICompatible,
        "../ManagedCode.LlmTck.Service/ManagedCode.LlmTck.Service.csproj"
    )
    .WithEndpoint("https://api.example.com/v1")
    .WithOpenAICompatibility()
    .WithApiKey("test-key");

builder.Build().Run();
