using ManagedCode.LlmTck.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddLlmTck()
    .WithApiKey("test-key");

builder.Build().Run();
