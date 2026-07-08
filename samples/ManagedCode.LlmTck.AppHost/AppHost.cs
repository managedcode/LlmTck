using ManagedCode.LlmTck.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddLlmTck()
    .WithOpenAICompatibility()
    .WithApiKey("test-key");

builder.Build().Run();
