using ManagedCode.LlmTck.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddLlmTck("llm-tck", "../ManagedCode.LlmTck.Service/ManagedCode.LlmTck.Service.csproj")
    .WithOpenAICompatibility()
    .WithApiKey("test-key");

builder.Build().Run();
