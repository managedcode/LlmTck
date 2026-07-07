using ManagedCode.LlmTck.Hosting;
using ManagedCode.LlmTck.Models;

var builder = WebApplication.CreateBuilder(args);

var endpoint = builder.Configuration["LlmTck:Endpoint"];
var openAiCompatibility = builder.Configuration["LLM_TCK_OPENAI_COMPATIBILITY"];
var requiredBearerToken = builder.Configuration["LlmTck:RequiredBearerToken"];
builder.Services.AddLlmTck(options =>
{
    options.AddModel("llm-tck-chat", LlmTckModelKind.Chat);
    options.AddModel("llm-tck-embedding", LlmTckModelKind.Embedding);
    options.AddModel("llm-tck-image", LlmTckModelKind.Image);
    options.AddModel("llm-tck-audio", LlmTckModelKind.Audio);

    if (!string.IsNullOrWhiteSpace(requiredBearerToken))
    {
        options.RequireBearerToken(requiredBearerToken);
    }

    options.AddChatScenario(
        "default-blue-whale",
        scenario => scenario
            .ForModel("llm-tck-chat")
            .WhenUserContains("color")
            .Responds("blue whale", "blue ", "whale")
            .Responds("blue whale", "blue ", "whale")
    );
});

var app = builder.Build();

app.MapGet(
    "/",
    () =>
        Results.Ok(
            new
            {
                name = "LLM TCK",
                status = "ready",
                endpoint,
                openAiCompatibility,
            }
        )
);
app.MapLlmTck();
app.Run();
