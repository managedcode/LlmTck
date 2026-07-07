using ManagedCode.LlmTck.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddLlmTck()
    .WithEndpoint("https://api.example.com/v1")
    .WithOpenAICompatibility()
    .WithApiKey("test-key");

builder.Build().Run();
