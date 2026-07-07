using ManagedCode.LlmTck.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddLlmTck("openai-compatible", "../ManagedCode.LlmTck.Service/ManagedCode.LlmTck.Service.csproj")
    .WithEndpoint("https://api.example.com/v1")
    .WithOpenAICompatibility()
    .WithApiKey("test-key");

builder.Build().Run();
